using System;
using System.IO;
using System.Collections.Generic;

namespace OpenFileFromDir
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var root = @"/home/ibob/prj/yama";
                string[] recent = { @"/home/ibob/prj/yama/test/unit/vector2.cpp", @"/home/ibob/prj/yama/test/unit/vector3.cpp", @"/home/ibob/prj/yama/test/unit/quaternion.cpp" };
                var w = new FileListWorker(root);
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
