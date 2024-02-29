using System;
using System.Text;
using GetSomeInput;
using PQueue;

namespace Test
{
    public static class Program
    {
        private static bool _RunForever = true;
        private static PersistentQueue _Queue = new PersistentQueue("./temp/", true);

        public static void Main(string[] args)
        {
            _Queue.DataQueued += (s, key) => Console.WriteLine("Data queued: " + key);
            _Queue.DataDequeued += (s, key) => Console.WriteLine("Data dequeued: " + key);
            _Queue.DataDeleted += (s, key) => Console.WriteLine("Data deleted: " + key);
            _Queue.DataExpired += (s, key) => Console.WriteLine("Data expired: " + key);
            _Queue.QueueCleared += (s, _) => Console.WriteLine("Queue cleared");
            
            while (_RunForever)
            {
                string input = Inputty.GetString("Command [?/help]:", null, false);

                switch (input)
                {
                    case "q":
                        _RunForever = false;
                        break;

                    case "?":
                        Menu();
                        break;

                    case "cls":
                        Console.Clear();
                        break;

                    case "enqueue":
                        Enqueue();
                        break;

                    case "dequeue":
                        Dequeue();
                        break;

                    case "depth":
                        Console.WriteLine(_Queue.Depth);
                        break;

                    case "length":
                        Console.WriteLine(_Queue.Length + " bytes");
                        break;

                    case "purge":
                        Purge();
                        break;

                    case "expire":
                        Expire();
                        break;

                    case "getexp":
                        GetExpiration();
                        break;

                    case "clear":
                        _Queue.Clear();
                        break;
                }
            }

            _Queue.Dispose();
        }

        private static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("   q          quit");
            Console.WriteLine("   ?          help, this menu");
            Console.WriteLine("   cls        clear the screen");
            Console.WriteLine("   enqueue    add to the queue");
            Console.WriteLine("   dequeue    read from the queue");
            Console.WriteLine("   depth      show the queue depth");
            Console.WriteLine("   length     show the queue length in bytes");
            Console.WriteLine("   purge      purge record from queue");
            Console.WriteLine("   expire     expire a record and purge it from the queue");
            Console.WriteLine("   getexp     retrieve the expiration for a given record");
            Console.WriteLine("   clear      empty the queue");
            Console.WriteLine("");
        }

        private static void Enqueue()
        {
            string data = Inputty.GetString("Data:", null, true);
            if (String.IsNullOrEmpty(data)) return;

            Console.WriteLine("For expiration, use the form of MM/dd/yyyy HH:mm:ss.  Press ENTER for no expiration.");
            DateTime? expiration = Inputty.GetNullableDateTime("Expiration:");

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            string key = _Queue.Enqueue(bytes, expiration);
            Console.WriteLine("Key: " + key);
        }

        private static void Dequeue()
        {
            (string, byte[])? msg = _Queue.Dequeue();
            /*
            string key = Inputty.GetString("Key   :", null, true);
            bool purge = Inputty.GetBoolean("Purge :", false);
            (string, byte[])? ret = _Queue.Dequeue(key, purge);
            if (ret != null)
            {
                Console.WriteLine(ret.Value.Item1 + ": " + Encoding.UTF8.GetString(ret.Value.Item2));
            }
            */
        }

        private static void Purge()
        {
            string key = Inputty.GetString("Key:", null, true);
            if (String.IsNullOrEmpty(key)) return;

            _Queue.Purge(key);
        }

        private static void Expire()
        {
            string key = Inputty.GetString("Key:", null, true);
            if (String.IsNullOrEmpty(key)) return;

            _Queue.Expire(key);
        }

        private static void GetExpiration()
        {
            string key = Inputty.GetString("Key:", null, true);
            if (String.IsNullOrEmpty(key)) return;

            DateTime? expiry = _Queue.GetExpiration(key);
            if (expiry == null) Console.WriteLine("Not found");
            else Console.WriteLine(expiry.Value.ToString());
        }
    }
}