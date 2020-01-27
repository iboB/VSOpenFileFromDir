using System;
using System.IO;
using System.Collections.Generic;

namespace watcher
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var root = @"C:\temp";
                string[] recent = { @"C:\temp\test\unit\vector2.cpp", @"C:\temp\test\unit\vector3.cpp", @"C:\temp\test\unit\quaternion.cpp" };
                var w = new Worker(root);
                FilteredListProvider f = null;

                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (cmd == "q")
                    {
                        break;
                    }
                    else if (cmd == "load")
                    {
                        f = new FilteredListProvider(root, recent);
                        w.ProcessFiles((List<string> wfiles) => f.SetFiles(wfiles));
                        Console.WriteLine("loaded");
                    }
                    else
                    {
                        if (f == null) continue;
                        var list = f.GetFilteredEntries(cmd);
                        foreach (var e in list)
                        {
                            var relativePath = Path.GetDirectoryName(e.fullPath.Substring(root.Length + 1));
                            Console.WriteLine($"{e.filename} ({relativePath}) {e.matchType}");
                        }
                    }
                    Console.WriteLine();
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
