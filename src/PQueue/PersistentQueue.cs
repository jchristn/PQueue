using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PQueue
{
    /// <summary>
    /// Persistent queue.  Queued entries are backed on disk.
    /// </summary>
    public class PersistentQueue : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Number of entries waiting in the queue.
        /// </summary>
        public int Depth
        {
            get
            {
                _Semaphore.Wait();

                try
                {
                    return Directory.GetFiles(_Directory, "*", SearchOption.TopDirectoryOnly).Length;
                }
                finally
                {
                    _Semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Event handler for when data is queued.
        /// </summary>
        public EventHandler<string> DataQueued { get; set; }

        /// <summary>
        /// Event handler for when data is dequeued.
        /// </summary>
        public EventHandler<string> DataDequeued { get; set; }

        /// <summary>
        /// Event handler for when data is deleted.
        /// </summary>
        public EventHandler<string> DataDeleted { get; set; }

        /// <summary>
        /// Event handler for when the queue is cleared.
        /// </summary>
        public EventHandler QueueCleared { get; set; }

        #endregion

        #region Private-Members

        private readonly bool _ClearOnDispose = false;
        private SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string _Directory = null;
        private DirectoryInfo _DirectoryInfo = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="directory">Directory.</param>
        /// <param name="clearOnDispose">Clear the queue's contents on dispose.  This will delete queued data.</param>
        public PersistentQueue(string directory, bool clearOnDispose = false)
        {
            if (String.IsNullOrEmpty(directory)) throw new ArgumentNullException(nameof(directory));

            _Directory = directory;

            InitializeDirectory();

            _ClearOnDispose = clearOnDispose;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_ClearOnDispose)
            {
                Clear();
                Directory.Delete(_Directory);
            }

            _Directory = null;
            _DirectoryInfo = null;
            _Semaphore = null;
        }

        /// <summary>
        /// Add data to the queue.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>Key.</returns>
        public string Enqueue(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string key = Guid.NewGuid().ToString();

            _Semaphore.Wait();

            try
            {
                using (FileStream fs = new FileStream(GetKey(key), FileMode.OpenOrCreate, FileAccess.Write))
                {
                    fs.Write(data, 0, data.Length);
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataQueued?.Invoke(this, key);

            return key;
        }

        /// <summary>
        /// Add data to the queue asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Key.</returns>
        public async Task<string> EnqueueAsync(byte[] data, CancellationToken token = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string key = Guid.NewGuid().ToString();

            await _Semaphore.WaitAsync();

            try
            { 
                using (FileStream fs = new FileStream(GetKey(key), FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fs.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataQueued?.Invoke(this, key);

            return key;
        }

        /// <summary>
        /// Retrieve data from the queue.
        /// </summary>
        /// <param name="key">Key, if a specific key is needed.</param>
        /// <param name="purge">Boolean flag indicating whether or not the entry should be removed from the queue once read.</param>
        /// <returns>Data.</returns>
        public byte[] Dequeue(string key = null, bool purge = false)
        {
            byte[] ret = null;
            string actualKey = null;

            _Semaphore.Wait();

            try
            {
                if (String.IsNullOrEmpty(key))
                {
                    // Get latest
                    string latest = GetLatestKey();
                    if (String.IsNullOrEmpty(latest)) return null;
                    key = latest;

                    actualKey = GetKey(latest);
                    int size = GetFileSize(latest);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret = new byte[size];
                        fs.Read(ret, 0, size);
                    }
                }
                else
                {
                    // Get specific
                    if (!KeyExists(key)) throw new KeyNotFoundException("The specified key '" + key + "' does not exist.");
                    
                    actualKey = GetKey(key);
                    int size = GetFileSize(key);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret = new byte[size];
                        fs.Read(ret, 0, size);
                    }
                }

                if (purge)
                {
                    File.Delete(actualKey);
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDequeued?.Invoke(this, key);

            if (purge) DataDeleted?.Invoke(this, key);

            return ret;
        }

        /// <summary>
        /// Retrieve data from the queue asynchronously.
        /// </summary>
        /// <param name="key">Key, if a specific key is needed.</param>
        /// <param name="purge">Boolean flag indicating whether or not the entry should be removed from the queue once read.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data.</returns>
        public async Task<byte[]> DequeueAsync(string key = null, bool purge = false, CancellationToken token = default)
        {
            byte[] ret = null;
            string actualKey = null;

            await _Semaphore.WaitAsync();

            try
            {
                if (String.IsNullOrEmpty(key))
                {
                    // Get latest
                    string latest = GetLatestKey();
                    if (String.IsNullOrEmpty(latest)) return null;
                    key = latest;

                    actualKey = GetKey(latest);
                    int size = GetFileSize(latest);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret = new byte[size];
                        await fs.ReadAsync(ret, 0, size, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Get specific
                    if (!KeyExists(key)) throw new KeyNotFoundException("The specified key '" + key + "' does not exist.");
                    
                    actualKey = GetKey(key);
                    int size = GetFileSize(key);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret = new byte[size];
                        await fs.ReadAsync(ret, 0, size, token).ConfigureAwait(false);
                    }
                }

                if (purge)
                {
                    File.Delete(actualKey);
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDequeued?.Invoke(this, key);

            if (purge) DataDeleted?.Invoke(this, key);

            return ret;
        }

        /// <summary>
        /// Remove a specific entry from the queue.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Purge(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (!KeyExists(key)) throw new KeyNotFoundException("The specified key '" + key + "' does not exist.");
            string actualKey = GetKey(key);

            _Semaphore.Wait();

            try
            {
                File.Delete(actualKey);
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDeleted?.Invoke(this, key);
        }

        /// <summary>
        /// Destructively empty the queue.  This will delete all of the files in the directory.
        /// </summary>
        public void Clear()
        {
            _Semaphore.Wait();

            try
            {
                foreach (FileInfo file in _DirectoryInfo.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in _DirectoryInfo.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            QueueCleared?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Private-Methods

        private void InitializeDirectory()
        {
            _Directory = _Directory.Replace("\\", "/");
            if (!_Directory.EndsWith("/")) _Directory += "/";
            if (!Directory.Exists(_Directory)) Directory.CreateDirectory(_Directory);

            _DirectoryInfo = new DirectoryInfo(_Directory);
        }

        private string GetKey(string str)
        {
            return _Directory + str;
        }

        private string GetLatestKey()
        {
            FileInfo file = _DirectoryInfo.GetFiles()
             .OrderByDescending(f => f.LastWriteTime)
             .First();

            if (file != null) return file.Name;
            else return null;
        }

        private int GetFileSize(string str)
        {
            string key = GetKey(str);
            return (int)(new FileInfo(key).Length);
        }

        private bool KeyExists(string str)
        {
            return File.Exists(GetKey(str));
        }

        #endregion
    }
}