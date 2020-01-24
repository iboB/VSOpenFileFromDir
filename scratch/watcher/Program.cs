using System;

namespace watcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var w = new Worker("/home/ibob/temp");

            while (true)
            {
                Worker.Message msg;
                msg.a = Console.ReadLine();
                msg.b = Console.ReadLine();

                w.SendMessage(msg);

                if (msg.a == "q") break;
            }

            w.Join();
        }
    }
}
