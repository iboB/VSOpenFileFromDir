using System.IO;
using System.Collections.Generic;

namespace watcher
{
    class FilteredListProvider
    {
        FilteredListProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public void SetFiles(List<string> files)
        {
            _entries = new Entry[files.Count];
            for (int i=0; i<files.Count; ++i)
            {
                var file = files[i];
                _entries[i].fullPath = file;
                _entries[i].filename = Path.GetFileName(file);
            }
        }

        public struct FilteredEntry
        {
            public string fullPath;
            public string filename;
            public List<int> matchPositions;
            public int sortWeight;
            public bool recent;
        }

        List<FilteredEntry> ApplyFilter(string filter)
        {
            var ret = new List<FilteredEntry>();

            return ret;
        }

        private struct Entry
        {
            public string fullPath; // full path to file
            public string filename; // file name only
        }
        static bool Match(string str, string filter, List<int> positions)
        {
            positions.Clear();
            int istr = 0, iflt = 0;
            while (true)
            {
                if (iflt == filter.Length) return true; // we have a match
                if (istr == str.Length) return false; // string is exhausted - no match

                var cflt = filter[iflt];

                // the filter comes from the user
                // it can contain forward slashes which we want to match with backcslashes
                // here we "lie" that each forward slash is a backward slash
                if (cflt == Path.AltDirectorySeparatorChar) cflt = Path.DirectorySeparatorChar;

                var cstr = str[istr];

                if (char.ToLower(cflt) == char.ToLower(cstr))
                {
                    // match
                    positions.Add(istr);
                    ++iflt;
                    ++istr;
                }
                else
                {
                    // no match continue in str
                    ++istr;
                }
            }
        }

        readonly string _rootPath;
        Entry[] _entries;
    }
}
