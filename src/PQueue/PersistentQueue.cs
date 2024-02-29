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
                    string[] files = Directory.GetFiles(_Directory, "*", SearchOption.TopDirectoryOnly);
                    if (files != null)
                    {
                        files = files.Where(f => !Path.GetFileName(f).Equals(_ExpiryFile)).ToArray();
                        return files.Length;
                    }
                    else
                    {
                        return 0;
                    }
                }
                finally
                {
                    _Semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Number of bytes waiting in the queue.
        /// </summary>
        public long Length
        {
            get
            {
                _Semaphore.Wait();

                try
                {
                    return Task.Run(() =>
                    {
                        IEnumerable<FileInfo> files = _DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
                        if (files != null && files.Count() > 0)
                        {
                            files = files.Where(f => !Path.GetFileName(f.Name).Equals(_ExpiryFile));
                            return files.Sum(f => f.Length);
                        }
                        else
                        {
                            return 0;
                        }
                    
                    }).Result;
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
        /// Event handler for when data is expired.
        /// </summary>
        public EventHandler<string> DataExpired { get; set; }

        /// <summary>
        /// Event handler for when an exception is raised.
        /// </summary>
        public EventHandler<Exception> ExceptionEncountered { get; set; }

        /// <summary>
        /// Event handler for when the queue is cleared.
        /// </summary>
        public EventHandler QueueCleared { get; set; }

        /// <summary>
        /// Name of the expiration file.  This file will live in the same directory as queued objects.
        /// </summary>
        public string ExpiryFile
        {
            get
            {
                return _ExpiryFile;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ExpiryFile));
                FileInfo fi = new FileInfo(value);
                _ExpiryFile = fi.Name;
            }
        }

        /// <summary>
        /// The number of milliseconds in between checks for expired files.
        /// </summary>
        public int ExpirationIntervalMs
        {
            get
            {
                return _ExpirationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ExpirationIntervalMs));
                _ExpirationIntervalMs = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly bool _ClearOnDispose = false;
        private SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);
        private string _Directory = null;
        private DirectoryInfo _DirectoryInfo = null;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private string _ExpiryFile = ".expire";
        private readonly object _ExpiryFileLock = new object();
        private int _ExpirationIntervalMs = 500;
        private Task _ExpirationTask = null;

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

            _ExpirationTask = Task.Run(() => ExpirationTask(_TokenSource.Token), _TokenSource.Token);
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
            _TokenSource.Cancel();
            _ExpirationTask = null;
            _ExpiryFile = null;
        }

        /// <summary>
        /// Add data to the queue.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="expiration">Timestamp at which the data should be expired from the queue.</param>
        /// <returns>Key.</returns>
        public string Enqueue(string data, DateTime? expiration = null)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Enqueue(Encoding.UTF8.GetBytes(data), expiration);
        }

        /// <summary>
        /// Add data to the queue.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="expiration">Timestamp at which the data should be expired from the queue.</param>
        /// <returns>Key.</returns>
        public string Enqueue(byte[] data, DateTime? expiration = null)
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

            if (expiration != null)
                AddExpiredObject(key, expiration.Value);

            DataQueued?.Invoke(this, key);

            return key;
        }

        /// <summary>
        /// Add data to the queue asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="expiration">Timestamp at which the data should be expired from the queue.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Key.</returns>
        public async Task<string> EnqueueAsync(string data, DateTime? expiration = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await EnqueueAsync(Encoding.UTF8.GetBytes(data), expiration, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Add data to the queue asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="expiration">Timestamp at which the data should be expired from the queue.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Key.</returns>
        public async Task<string> EnqueueAsync(byte[] data, DateTime? expiration = null, CancellationToken token = default)
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

            if (expiration != null)
                AddExpiredObject(key, expiration.Value);

            DataQueued?.Invoke(this, key);

            return key;
        }

        /// <summary>
        /// Retrieve data from the queue.
        /// </summary>
        /// <param name="key">Key, if a specific key is needed.</param>
        /// <param name="purge">Boolean flag indicating whether or not the entry should be removed from the queue once read.</param>
        /// <returns>Data.</returns>
        public (string, byte[])? Dequeue(string key = null, bool purge = false)
        {
            (string, byte[]) ret;
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
                    ret.Item1 = latest;

                    actualKey = GetKey(latest);
                    int size = GetFileSize(latest);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret.Item2 = new byte[size];
                        fs.Read(ret.Item2, 0, size);
                    }
                }
                else
                {
                    // Get specific
                    if (!KeyExists(key)) throw new KeyNotFoundException("The specified key '" + key + "' does not exist.");

                    ret.Item1 = key;
                    actualKey = GetKey(key);
                    int size = GetFileSize(key);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret.Item2 = new byte[size];
                        fs.Read(ret.Item2, 0, size);
                    }
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDequeued?.Invoke(this, key);

            if (purge)
            {
                Purge(key);
                RemoveExpiredObject(key);
            }

            return ret;
        }

        /// <summary>
        /// Retrieve data from the queue asynchronously.
        /// </summary>
        /// <param name="key">Key, if a specific key is needed.</param>
        /// <param name="purge">Boolean flag indicating whether or not the entry should be removed from the queue once read.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data.</returns>
        public async Task<(string, byte[])?> DequeueAsync(string key = null, bool purge = false, CancellationToken token = default)
        {
            (string, byte[]) ret;
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
                    ret.Item1 = latest;

                    actualKey = GetKey(latest);
                    int size = GetFileSize(latest);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret.Item2 = new byte[size];
                        await fs.ReadAsync(ret.Item2, 0, size, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Get specific
                    if (!KeyExists(key)) throw new KeyNotFoundException("The specified key '" + key + "' does not exist.");

                    ret.Item1 = key;
                    actualKey = GetKey(key);
                    int size = GetFileSize(key);

                    using (FileStream fs = new FileStream(actualKey, FileMode.Open, FileAccess.Read))
                    {
                        ret.Item2 = new byte[size];
                        await fs.ReadAsync(ret.Item2, 0, size, token).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDequeued?.Invoke(this, key);

            if (purge)
            {
                Purge(key);
                RemoveExpiredObject(key);
            }

            return ret;
        }

        /// <summary>
        /// Remove a specific entry from the queue.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Purge(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string actualKey = GetKey(key);

            _Semaphore.Wait();

            try
            {
                if (File.Exists(actualKey)) File.Delete(actualKey);
            }
            finally
            {
                _Semaphore.Release();
            }

            DataDeleted?.Invoke(this, key);
        }

        /// <summary>
        /// Remove a specific entry due to expiration.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Expire(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            RemoveExpiredObject(key);
            Purge(key);
        }

        /// <summary>
        /// Retrieve the expiration timestamp for a given key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Nullable DateTime.</returns>
        public DateTime? GetExpiration(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            return GetObjectExpiration(key);
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
            IOrderedEnumerable<FileInfo> files = _DirectoryInfo.GetFiles()
                .Where(f => !f.Name.Equals(_ExpiryFile))
                .OrderByDescending(f => f.LastWriteTime);
 
            if (files != null && files.Count() > 0) return files.ToArray()[0].Name;
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

        private IEnumerable<KeyValuePair<string, DateTime>> EnumerateExpiringObjects()
        {
            string[] lines = null;
            string filename = GetKey(_ExpiryFile);

            lock (_ExpiryFileLock)
            {
                lines = File.ReadAllLines(GetKey(_ExpiryFile));
            }

            if (lines != null && lines.Length > 0)
            {
                for (int i = 0; i < lines.Length; i++) 
                {
                    KeyValuePair<string, DateTime>? kvp = ParseExpiryFileLine(i, lines[i]);
                    if (kvp == null)
                    {
                        ExceptionEncountered?.Invoke(this, new ArgumentException("Invalid line format detected in line " + i + " of expiry file " + filename));
                        continue;
                    }

                    yield return kvp.Value;
                }
            }

            yield break;
        }

        private void AddExpiredObject(string key, DateTime expiration)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string filename = GetKey(_ExpiryFile);

            List<string> updatedLines = new List<string>();

            lock (_ExpiryFileLock)
            {
                string[] lines = File.ReadAllLines(filename);

                if (lines != null && lines.Length > 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        KeyValuePair<string, DateTime>? line = ParseExpiryFileLine(i, lines[i]);
                        if (line == null) continue;
                        if (!line.Value.Key.Equals(key)) updatedLines.Add(line.Value.Key + " " + line.Value.Value.ToString());
                    }
                }

                updatedLines.Add(key + " " + expiration.ToString());
                File.Delete(filename);
                File.WriteAllLines(filename, updatedLines);
            }
        }

        private DateTime? GetObjectExpiration(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string filename = GetKey(_ExpiryFile);

            lock (_ExpiryFileLock)
            {
                string[] lines = File.ReadAllLines(filename);

                if (lines != null && lines.Length > 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        KeyValuePair<string, DateTime>? line = ParseExpiryFileLine(i, lines[i]);
                        if (line == null) continue;
                        if (line.Value.Key.Equals(key)) return line.Value.Value;
                    }
                }

                return null;
            }
        }

        private void RemoveExpiredObject(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string filename = GetKey(_ExpiryFile);

            List<string> updatedLines = new List<string>();

            lock (_ExpiryFileLock)
            {
                string[] lines = File.ReadAllLines(filename);

                if (lines != null && lines.Length > 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        KeyValuePair<string, DateTime>? line = ParseExpiryFileLine(i, lines[i]);
                        if (line == null) continue;
                        if (!line.Value.Key.Equals(key)) updatedLines.Add(line.Value.Key);
                        else DataExpired?.Invoke(this, key);
                    }
                }

                File.Delete(filename);
                File.WriteAllLines(filename, updatedLines);
            }
        }

        private async Task ExpirationTask(CancellationToken token = default)
        {
            string filename = GetKey(_ExpiryFile);

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_ExpirationIntervalMs, token).ConfigureAwait(false);

                if (!File.Exists(filename))
                    File.WriteAllBytes(filename, Array.Empty<byte>());

                try
                {
                    foreach (KeyValuePair<string, DateTime> expired in EnumerateExpiringObjects())
                    {
                        if (token.IsCancellationRequested) break;

                        if (expired.Value < DateTime.Now)
                        {
                            RemoveExpiredObject(expired.Key);
                            Purge(expired.Key);
                        }
                    }
                }
                catch (TaskCanceledException)
                {

                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception e)
                {
                    ExceptionEncountered?.Invoke(this, e);
                }
            }
        }

        private KeyValuePair<string, DateTime>? ParseExpiryFileLine(int lineNumber, string line)
        {
            string[] parts = line.Split(new char[] { ' ' }, 2);
            string filename = GetKey(_ExpiryFile);

            if (parts.Length != 2)
            {
                ExceptionEncountered?.Invoke(
                    this, 
                    new ArgumentException("Invalid line format detected in line " + lineNumber + " of expiry file " + filename));

                return null;
            }

            DateTime expires = DateTime.Now;
            try
            {
                expires = DateTime.Parse(parts[1]);
            }
            catch (Exception)
            {
                ExceptionEncountered?.Invoke(
                    this, 
                    new ArgumentException("Invalid DateTime format detected in line " + lineNumber + " of expiry file " + filename));

                return null;
            }

            return new KeyValuePair<string, DateTime>(parts[0], expires);
        }

        #endregion
    }
}