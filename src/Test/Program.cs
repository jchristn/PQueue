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
            /*
            _Queue.DataQueued += (s, a) => Console.WriteLine("Data queued: " + a);
            _Queue.DataDequeued += (s, a) => Console.WriteLine("Data dequeued: " + a);
            _Queue.DataDeleted += (s, a) => Console.WriteLine("Data deleted: " + a);
            _Queue.QueueCleared += (s, a) => Console.WriteLine("Queue cleared");
             */

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

                    case "purge":
                        Purge();
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
            Console.WriteLine("   purge      purge record from queue");
            Console.WriteLine("   clear      empty the queue");
            Console.WriteLine("");
        }

        private static void Enqueue()
        {
            string data = Inputty.GetString("Data:", null, true);
            if (String.IsNullOrEmpty(data)) return;

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            string key = _Queue.Enqueue(bytes);
            Console.WriteLine("Key: " + key);
        }

        private static void Dequeue()
        {
            string key = Inputty.GetString("Key   :", null, true);
            bool purge = Inputty.GetBoolean("Purge :", false);
            byte[] bytes = _Queue.Dequeue(key, purge);
            if (bytes != null && bytes.Length > 0)
            {
                Console.WriteLine(Encoding.UTF8.GetString(bytes));
            }
        }

        private static void Purge()
        {
            string key = Inputty.GetString("Key:", null, true);
            if (String.IsNullOrEmpty(key)) return;

            _Queue.Purge(key);
        }
    }
}