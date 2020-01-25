using System;

namespace watcher
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var w = new Worker("/home/ibob/temp");

                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (cmd == "q")
                    {
                        break;
                    }
                    else if (cmd == "l")
                    {
                        var files = w.GetFiles();
                        Console.WriteLine(string.Join('\n', files));
                        Console.WriteLine();
                    }
                }

                w.Join();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e}");
            }
        }
    }
}
