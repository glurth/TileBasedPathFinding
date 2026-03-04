using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace EyE.Threading
{

    /// <summary>
    /// Thread-safe reference to a float value, for reporting progress across threads.
    /// </summary>
    public class ProgressFloatRef
    {
        private float value = 0;
        private string stageMessage = "";
        private readonly object lockObj = new object();

        public void Increment(float incrementAmount)
        {
            lock (lockObj) this.value += incrementAmount;
        }

        public float Value
        {
            get { lock (lockObj) return value; }
            set { lock (lockObj) this.value = value; }
        }
        public string StageMessage
        {
            get { lock (lockObj) return stageMessage; }
            set { lock (lockObj) this.stageMessage = value; }
        }

    }

    /// <summary>
    /// Wraps a <see cref="CancellationSource"/> and <see cref="ProgressFloatRef"/> for easy task management.
    /// Contains an internal YieldTimer and public yield function to use it and check for cancellation requests.
    /// </summary>
    public class TaskHandler : System.IDisposable
    {
        #region construction
        /// <summary>
        /// Initializes a new instance of <see cref="TaskHandler"/>.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to observe.</param>
        /// <param name="progress">The progress reference to report progress.</param>
        public TaskHandler(UniTask task, CancellationTokenSource cancellationToken, ProgressFloatRef progress)
        {

            CancellationSource = cancellationToken;
            ownsCancellationSource = false;
            this.task = task;
            this.progress = progress;
            isAsynchrnousProcess = true;
            taskSet = true;
        }
        /// <summary>
        /// Initializes a new instance of <see cref="TaskHandler"/>.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to observe.</param>
        /// <param name="progress">The progress reference to report progress.</param>
        public TaskHandler(UniTask task, ProgressFloatRef progress)
        {
            CancellationSource = new CancellationTokenSource();
            ownsCancellationSource = true;
            this.task = task;
            this.progress = progress;
            isAsynchrnousProcess = true;
            taskSet = true;
        }
        /// <summary>
        /// Initializes a new instance of <see cref="TaskHandler"/>.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to observe.</param>
        /// <param name="progress">The progress reference to report progress.</param>
        public TaskHandler(UniTask task)
        {
            CancellationSource = new CancellationTokenSource();
            ownsCancellationSource = true;
            this.task = task;
            this.progress = new ProgressFloatRef();
            isAsynchrnousProcess = true;
            taskSet = true;
        }
        //with asAsync set to false, process will be run synchronously and never invoke internalYieldControl.Yield
        public TaskHandler(bool asAsync = true)
        {
            isAsynchrnousProcess = asAsync;
            CancellationSource = new CancellationTokenSource();
            ownsCancellationSource = true;
            this.progress = new ProgressFloatRef();
        }

        private Func<UniTask> deferredTaskFunction=null;
        private bool isDeferred => deferredTaskFunction != null;

        // Constructor for Deferred Execution
        public TaskHandler(Func<UniTask> taskFunctionRef, ProgressFloatRef progress = null)
        {
            this.deferredTaskFunction = taskFunctionRef;
            this.progress = progress ?? new ProgressFloatRef();
            this.CancellationSource = new CancellationTokenSource();
            this.ownsCancellationSource = true;
            this.isAsynchrnousProcess = true;
            this.taskSet = false; // Task isn't "set" yet because it hasn't run
        }
        #endregion

        #region Disposal
        readonly bool ownsCancellationSource;
        private bool disposed;
        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(TaskHandler));
        }
        /// <summary>
        /// Not that Disposing a running task will NOT instantly stop the process.  Rather the process will be stopped, and the disposal will complete, only when the running task next invokes the Yield function.
        /// </summary>

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            deferredTaskFunction = null;

            if (ownsCancellationSource && CancellationSource != null)
            {
                // Signal the save logic to stop
                try { CancellationSource.Cancel(); } catch { }

                // If nothing is running, we can kill the CTS now.
                // IF something is running, we CANNOT dispose the CTS yet.
                if (!IsRunning)
                {
                    CancellationSource.Dispose();
                }
                else
                {
                    // We let the RUNNING task dispose the CTS when it finally exits.
                    // This avoids the "await twice" error entirely.
                }
            }
        }


        #endregion

        #region member variables

        /// <summary>
        /// Gets the cancellation token associated with this task context.
        /// </summary>
        /// 
        public bool IsAsynchrnousProcess { get { return isAsynchrnousProcess; } }
        public bool IsComplete => isComplete;
        public bool IsRunning
        {
            get
            {
               // return taskSet && task.Status == UniTaskStatus.Pending;
                if (!taskSet) return false;
                return !IsComplete && task.Status != UniTaskStatus.Canceled && task.Status != UniTaskStatus.Faulted && task.Status != UniTaskStatus.Succeeded;
            }
        }
        /// <summary>
        /// Gets whether cancellation has been requested, or if the object has been disposed.
        /// </summary>
        public bool IsCancellationRequested
        {
            get
            {
                if (disposed) return true;
                return CancellationSource.IsCancellationRequested;
            }
        }
        /// <summary>
        /// Gets or sets the progress value.
        /// </summary>
        public float ProgressValue
        {
            get => progress.Value;
            set => progress.Value = value;
        }
        /// <summary>
        /// Gets or sets the stage message.
        /// </summary>
        public string StageMessage
        {
            get { lock (lockObj) return progress.StageMessage; }
            set { lock (lockObj) progress.StageMessage = value; }
        }
        //private
        readonly ProgressFloatRef progress;
        CancellationTokenSource CancellationSource { get; }
        UniTask task;
        public UniTask Task { get => task; }
        bool taskSet = false;
        YieldTimer internalYieldControl = new YieldTimer();
        bool isComplete = false;
        readonly bool isAsynchrnousProcess = false;
        object lockObj = new object();

        #endregion

        #region control functions
        public void AssignRunningTask(UniTask tsk)
        {
            if (taskSet)
              throw new System.Exception("You may not pass a task to a TaskHandler that has already been assigned one");
            task = tsk;
            taskSet = true;


        }
        public void AssignDeferredTask(Func<UniTask> taskFunctionRef)
        {
            if (taskSet)
                throw new System.Exception("You may not pass a task to a TaskHandler that has already been assigned one");
            deferredTaskFunction = taskFunctionRef;
            taskSet = false;
        }
        public async UniTask AwaitTaskAsync()
        {
            ThrowIfDisposed();

            try
            {
                if (deferredTaskFunction != null && !taskSet)
                {
                    task = deferredTaskFunction.Invoke();
                    taskSet = true;
                    await task;
                }
                else if (taskSet)
                {
                    while (task.Status == UniTaskStatus.Pending)
                        await Yield();

                    if (task.Status == UniTaskStatus.Faulted)
                        await task;
                }
            }
            finally
            {
                isComplete = true;
                // If the handler was disposed while we were working, 
                // we clean up the CTS now that we are officially done.
                if (disposed && ownsCancellationSource)
                {
                    CancellationSource?.Dispose();
                }
            }
        }

        public async UniTask Yield()
        {
            if (!IsAsynchrnousProcess) return;
            CancellationSource.Token.ThrowIfCancellationRequested();
            await internalYieldControl.YieldOnTimeSlice();
        }
        public void SetComplete(){ isComplete = true;  }

        public void IncrementProgress(float incrementAmount)
        {
            progress.Increment(incrementAmount);
        }


        public async UniTask SetStageMessageAndYield(string message)
        {
            StageMessage = message;
            await Yield();
            //await UniTask.NextFrame();
            //await UniTask.SwitchToThreadPool();
        }
        /// <summary>
        /// Reports progress and optionally updates the stage message atomically.
        /// </summary>
        /// <param name="value">The progress value.</param>
        /// <param name="message">The optional stage message.</param>
        public void Report(float value, string message = null)
        {
            progress.Value = value;
            if (message != null)
                progress.StageMessage = message;
        }

        /// <summary>
        /// Throws an <see cref="OperationCanceledException"/> if the cancellation has been requested.
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            ThrowIfDisposed();
            CancellationSource.Token.ThrowIfCancellationRequested();
        }


        public void DoCancel() { ThrowIfDisposed(); CancellationSource.Cancel(); }
        #endregion



        public void OnGUIDebug()
        {
            UnityEngine.GUILayout.BeginVertical("box");
            UnityEngine.GUILayout.Label("TaskContext Debug");

            UnityEngine.GUILayout.Label($"IsComplete: {IsComplete}");
            UnityEngine.GUILayout.Label($"IsRunning: {IsRunning}");
            UnityEngine.GUILayout.Label($"IsCancellationRequested: {IsCancellationRequested}");

            if (task.Status == UniTaskStatus.Pending)
                UnityEngine.GUILayout.Label("Task Status: Pending");
            else if (task.Status == UniTaskStatus.Succeeded)
                UnityEngine.GUILayout.Label("Task Status: Succeeded");
            else if (task.Status == UniTaskStatus.Faulted)
                UnityEngine.GUILayout.Label("Task Status: Faulted");
            else if (task.Status == UniTaskStatus.Canceled)
                UnityEngine.GUILayout.Label("Task Status: Canceled");

            UnityEngine.GUILayout.Label($"Progress: {ProgressValue:0.00}");
            UnityEngine.GUILayout.Label($"StageMessage: {StageMessage}");

            UnityEngine.GUILayout.Label($"CancellationSource disposed: {disposed}");
            UnityEngine.GUILayout.EndVertical();
        }


    }

    /// <summary>
    /// Utility class to help throttle async task execution by yielding control
    /// back to Unity after a specified time slice has elapsed.
    /// Set the runSynchronously constructor param to override, and never actually yield.  (this allows calling code to stay the same, but handle both sync and async)
    /// </summary>
    public class YieldTimer
    {
        private readonly int timeSlice; // Time slice in milliseconds
        private readonly System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        //private readonly CancelBoolRef cancelRef; // Optional cancellation reference
        private readonly bool runSynchronously;

        public YieldTimer(int timeSlice = 10, bool runSynchronously = false)
        {
            this.timeSlice = timeSlice;
          //  this.cancelRef = cancelRef;
            this.runSynchronously = runSynchronously;
            if (!runSynchronously) timer.Start();
        }
        public YieldTimer(bool runSynchronously)
        {
            this.timeSlice = 10;
            //this.cancelRef = cancelRef;
            this.runSynchronously = runSynchronously;
            if (!runSynchronously) timer.Start();
        }
        /// <summary>
        /// If the processing timer has reached or exceeded the allotted time-slice, will yield until the next unity update.
        /// </summary>
        /// <returns></returns>
        public async UniTask YieldOnTimeSlice()
        {
            if (runSynchronously) return;
            //if (cancelRef != null && cancelRef.doCancel)
              //  throw new System.OperationCanceledException();
            if (timer.ElapsedMilliseconds > timeSlice)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                timer.Restart(); // Restart timer after yielding
            }
        }
    }
    
}