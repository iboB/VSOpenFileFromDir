using System;
using System.Collections.Generic;

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
                        string[] files = null;
                        w.ProcessFiles((List<string> wfiles) => files = wfiles.ToArray());
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
