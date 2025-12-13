using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace EyE.Threading
{
    /// <summary>
    /// TODO: replace with TaskContext or CancelationSource/token  provide a way to reference a single/the same bool- stored in here- regardless of thread context
    /// </summary>
    public class OLDCancelBoolRef
    {
        public volatile bool doCancel = false;
    }

    /// <summary>
    /// Thread-safe reference to a float value, for reporting progress across threads.
    /// </summary>
    public class ProgressFloatRef
    {
        private float value=0;
        private string stageMessage="";
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
    public class TaskHandler:System.IDisposable
    {
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
            IsAsynchrnousProcess = true;
        }
        //with asAsync set to false, process will be run synchronously and never invoke internalYieldControl.Yield
        public TaskHandler(bool asAsync = true)
        {
            IsAsynchrnousProcess = asAsync;
            CancellationSource = new CancellationTokenSource();
            ownsCancellationSource = true;
            this.progress = new ProgressFloatRef();
        }

        readonly bool ownsCancellationSource;
        bool disposed;

        private readonly ProgressFloatRef progress;

        /// <summary>
        /// Gets the cancellation token associated with this task context.
        /// </summary>
        CancellationTokenSource CancellationSource { get; }

        public UniTask task;
        public bool IsAsynchrnousProcess = false;

        YieldTimer internalYieldControl = new YieldTimer();

        public async UniTask Yield()
        {
            if (!IsAsynchrnousProcess) return;
            CancellationSource.Token.ThrowIfCancellationRequested();
            await internalYieldControl.YieldOnTimeSlice();
        }

        private bool isComplete=false;
        public bool IsComplete => isComplete;
        public void SetComplete(){ isComplete = true;  }

        public bool IsRunning { get { return !IsComplete &&  task.Status != UniTaskStatus.Canceled && task.Status != UniTaskStatus.Faulted && task.Status != UniTaskStatus.Succeeded; } }

        /// <summary>
        /// Gets or sets the progress value.
        /// </summary>
        public float ProgressValue
        {
            get => progress.Value;
            set => progress.Value = value;
        }

        public void IncrementProgress(float incrementAmount)
        {
            progress.Increment(incrementAmount);
        }
        object lockObj = new object();
        /// <summary>
        /// Gets or sets the stage message.
        /// </summary>
        public string StageMessage
        {
            get { lock (lockObj) return progress.StageMessage; }
            set { lock (lockObj) progress.StageMessage = value; }
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
            CancellationSource.Token.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Gets whether cancellation has been requested.
        /// </summary>
        public bool IsCancellationRequested => CancellationSource.IsCancellationRequested;
        public void DoCancel() { CancellationSource.Cancel(); }


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
            // Detect if the CancellationTokenSource has been disposed.
            // There is no public "IsDisposed" property, so the only safe way
            // to check is to attempt accessing a property (here IsCancellationRequested)
            // and catch ObjectDisposedException if it's disposed.
            bool disposed = false;

            try
            {
                CancellationSource.Token.ThrowIfCancellationRequested();
            }
            catch (System.ObjectDisposedException)
            {
                disposed = true;
            }
            catch (System.OperationCanceledException)
            {
                // Token was canceled normally, CTS is still valid
            }
            UnityEngine.GUILayout.Label($"CancellationSource disposed: {disposed}");
            UnityEngine.GUILayout.EndVertical();
        }

        public void Dispose()
        {
            UnityEngine.Debug.Log("Disposing CancelationSource on completion now");
            if (disposed) return;
            disposed = true;

            if (ownsCancellationSource)
                CancellationSource.Dispose();
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