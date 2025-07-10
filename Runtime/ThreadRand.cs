using UnityEngine;
using Cysharp.Threading.Tasks;
namespace EyE.Threading
{
    /// <summary>
    /// Thread-safe source of Mathf.Pow(Random.value, 2) values usable in background threads.
    /// Lazily initializes on first access. UnityEngine.Random is used only on the main thread.
    /// </summary>
    public static class ThreadRand
    {
        static float[] _buffer;
        static int _index;
        static readonly object _lock = new object();
        const int BufferSize = 1024;

        /// <summary>
        /// Returns the next precomputed random value (biased toward 0 via pow2).
        /// Ensures initialization runs on the main thread.
        /// </summary>
        public static async UniTask<float> GetRandAsync()
        {
            await InitAsync();

            lock (_lock)
            {
                float val = _buffer[_index++];
                if (_index < _buffer.Length)
                    return val;
            }

            await Refill();

            lock (_lock)
            {
                _index = 0;// just incase cuz threads
                return _buffer[_index++];
            }
        }

        /// <summary>
        /// Initializes the buffer on the main thread if it hasn't been initialized yet.
        /// Safe to call concurrently.
        /// </summary>
        static async UniTask InitAsync()
        {
            if (_buffer != null) return;

            await UniTask.SwitchToMainThread();

            float[] temp = new float[BufferSize];
            for (int i = 0; i < BufferSize; i++)
                temp[i] = Random.value;

            lock (_lock)// thread safely populate the buffer member
            {
                if (_buffer == null)
                {
                    _buffer = temp;
                    _index = 0;
                }
            }
            await UniTask.SwitchToThreadPool();
        }

        /// <summary>
        /// Refills the buffer with fresh pow2-random values. Must be called on main thread.
        /// </summary>
        static async UniTask Refill()
        {
            await UniTask.SwitchToMainThread();
            for (int i = 0; i < _buffer.Length; i++)
                _buffer[i] = UnityEngine.Random.value;

            _index = 0;
            await UniTask.SwitchToThreadPool();
        }
    }
}