using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace watcher
{
    class Worker
    {
        public struct Message
        {
            public string a;
            public string b;
        }
        private FileSystemWatcher _watcher = null;
        private Thread _thread;
        private BlockingCollection<Message> _messageQueue;
        public Worker(string path)
        {
            _messageQueue = new BlockingCollection<Message>();
            _thread = new Thread(new ThreadStart(this.Run));
            _thread.Start();
        }

        public void Join()
        {
            _thread.Join();
        }

        private void Run()
        {
            while (true)
            {
                var msg = _messageQueue.Take();
                Console.WriteLine($"{msg.a} - {msg.b}");
                if (msg.a == "q") break;
            }
        }

        public void SendMessage(Message msg)
        {
            _messageQueue.Add(msg);
        }
    }
}