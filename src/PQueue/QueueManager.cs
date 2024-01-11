using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PQueue
{
    /// <summary>
    /// Queue manager.
    /// </summary>
    public class QueueManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private Dictionary<string, PersistentQueue> _Queues = new Dictionary<string, PersistentQueue>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QueueManager()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a queue using the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="queue">Queue.</param>
        public void Add(string key, PersistentQueue queue)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (queue == null) throw new ArgumentNullException(nameof(queue));

            lock (_Lock)
            {
                if (!_Queues.ContainsKey(key))
                {
                    _Queues.Add(key, queue);
                }
                else
                {
                    throw new InvalidOperationException("A queue already exists with key '" + key + "'.");
                }
            }
        }

        /// <summary>
        /// Add a queue using the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="queue">Queue.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool TryAdd(string key, PersistentQueue queue)
        {
            try
            {
                Add(key, queue);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Remove a queue using the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        public void Remove(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_Lock)
            {
                if (_Queues.ContainsKey(key))
                {
                    _Queues.Remove(key);
                }
                else
                {
                    throw new KeyNotFoundException("A queue with key '" + key + "' could not be found.");
                }
            }
        }

        /// <summary>
        /// Remove a queue using the specified key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool TryRemove(string key)
        {
            try
            {
                Remove(key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a queue exists by key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Boolean indicating existence.</returns>
        public bool Exists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_Lock)
            {
                return _Queues.ContainsKey(key);
            }
        }

        /// <summary>
        /// Retrieve a queue by key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Queue.</returns>
        public PersistentQueue Get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_Lock)
            {
                if (_Queues.ContainsKey(key))
                {
                    return _Queues[key];
                }
                else
                {
                    throw new KeyNotFoundException("A queue with key '" + key + "' could not be found.");
                }
            }
        }

        /// <summary>
        /// Retrieve a queue by key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="queue">Queue.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool TryGet(string key, out PersistentQueue queue)
        {
            queue = null;

            try
            {
                queue = Get(key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
