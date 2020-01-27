using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenFileFromDir
{
    using DirFilters = HashSet<string>;
    using FileFilters = List<Regex>;

    public class FileListWorker
    {
        public FileListWorker(string rootPath)
        {
            if (!Directory.Exists(rootPath)) throw new DirectoryNotFoundException(rootPath);
            // normalize path. we don't want trailing directory separators
            if (rootPath.EndsWith("\\") || rootPath.EndsWith("/")) rootPath = rootPath.Remove(rootPath.Length - 1);
            _rootPath = rootPath;
            _messageQueue = new BlockingCollection<Msg>();
            _allFiles = new List<string>();

            // start the watchers before we start the thread and collect file infos
            // thus if any events happen while we're building the list, we'll have
            // the opportunity to reflect them
            _watcher = new FileSystemWatcher();
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Path = _rootPath;
            _watcher.Created += (object src, FileSystemEventArgs e) => SendMessage(Msg.Created(e.FullPath));
            _watcher.Deleted += (object src, FileSystemEventArgs e) => SendMessage(Msg.Deleted(e.FullPath));
            _watcher.Renamed += (object src, RenamedEventArgs e) => SendMessage(Msg.Renamed(e.OldFullPath, e.FullPath));
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            _filtersWatcher = new FileSystemWatcher();
            _filtersWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _filtersWatcher.Path = _rootPath;
            _filtersWatcher.Filter = FiltersFilename;
            _filtersWatcher.Created += (object src, FileSystemEventArgs e) => OnFiltersChanged();
            _filtersWatcher.Deleted += (object src, FileSystemEventArgs e) => OnFiltersChanged();
            _filtersWatcher.Changed += (object src, FileSystemEventArgs e) => OnFiltersChanged();
            _filtersWatcher.Renamed += (object src, RenamedEventArgs e) => OnFiltersChanged();
            _filtersWatcher.EnableRaisingEvents = true;

            _thread = new Thread(new ThreadStart(this.Run));
            _thread.Start();
        }

        public string GetRootPath() { return _rootPath; }
        public string[] GetFiles()
        {
            lock (_allFiles)
            {
                return _allFiles.ToArray();
            }
        }

        public void ProcessFiles(Action<List<string>> processor)
        {
            lock (_allFiles)
            {
                processor(_allFiles);
            }
        }

        // you must call this to join the worker's threads
        public void Join()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher = null;
            _filtersWatcher.EnableRaisingEvents = false;
            _filtersWatcher = null;
            SendMessage(Msg.Quit());
            _thread.Join();
            _thread = null;
        }

        private struct Msg
        {
            public enum Type
            {
                Created,
                Deleted,
                Renamed,
                FiltersChanged,
                Quit,
            }
            public Type type;
            public string path;
            public string oldPath;

            public static Msg Created(string path)
            {
                Msg msg;
                msg.type = Type.Created;
                msg.path = path;
                msg.oldPath = null;
                return msg;
            }

            public static Msg Deleted(string path)
            {
                Msg msg;
                msg.type = Type.Deleted;
                msg.path = path;
                msg.oldPath = null;
                return msg;
            }

            public static Msg Renamed(string oldPath, string newPath)
            {
                Msg msg;
                msg.type = Type.Renamed;
                msg.path = newPath;
                msg.oldPath = oldPath;
                return msg;
            }

            public static Msg FiltersChanged()
            {
                Msg msg;
                msg.type = Type.FiltersChanged;
                msg.path = null;
                msg.oldPath = null;
                return msg;
            }

            public static Msg Quit()
            {
                Msg msg;
                msg.type = Type.Quit;
                msg.path = null;
                msg.oldPath = null;
                return msg;
            }
        }
        private void SendMessage(Msg msg) => _messageQueue.Add(msg);

        private bool DirPassesFilter(string dir)
        {
            if (_dirFilters == null) return true;

            var elements = new HashSet<string>(dir.Substring(_rootPath.Length).Split(Path.DirectorySeparatorChar));

            return !elements.Overlaps(_dirFilters);
        }

        private bool FilePassesFilter(string file)
        {
            var dir = Path.GetDirectoryName(file);
            if (!DirPassesFilter(dir)) return false;

            if (_fileFilters == null) return true;

            var fname = Path.GetFileName(file);
            foreach (var filter in _fileFilters)
            {
                if (filter.IsMatch(fname)) return false;
            }
            return true;
        }

        void BuildTree(string path)
        {
            try
            {
                // first add files
                var files = Directory.EnumerateFiles(path);
                lock (_allFiles)
                {
                    foreach (var file in files)
                    {
                        if (FilePassesFilter(file))
                        {
                            _allFiles.Add(file);
                        }
                    }
                }

                // then recurse over dirs
                var dirs = Directory.EnumerateDirectories(path);
                foreach (var dir in dirs)
                {
                    if (DirPassesFilter(dir))
                    {
                        BuildTree(dir);
                    }
                }
            }
            catch
            {
                // ignore exceptions
                // most likely file or dir was deleted while we were parsing it
                // ot perhaps we don't have permissions
                // either way we don't care
            }
        }

        void BuildFullTree()
        {
            _allFiles.Clear();
            BuildTree(_rootPath);
        }

        void ReadFilters(out DirFilters dirFilters, out FileFilters fileFilters)
        {
            dirFilters = makeDefaultDirFilters();
            fileFilters = makeDefaultFileFilters();
            try
            {
                var fname = Path.Combine(_rootPath, FiltersFilename);
                if (!File.Exists(fname)) return;

                var text = File.ReadAllText(fname);
                using (var json = JsonDocument.Parse(text))
                {
                    var root = json.RootElement;
                    JsonElement dirs;
                    if (root.TryGetProperty("dirs", out dirs))
                    {
                        var edirs = dirs.EnumerateArray();
                        dirFilters = new DirFilters();
                        foreach (var d in edirs)
                        {
                            dirFilters.Add(d.GetString());
                        }
                    }
                    JsonElement files;
                    if (root.TryGetProperty("files", out files))
                    {
                        var efiles = files.EnumerateArray();
                        fileFilters = new FileFilters();
                        foreach(var f in efiles)
                        {
                            fileFilters.Add(makeFileFilter(f.GetString()));
                        }
                    }
                }
            }
            catch
            {
            }
        }

        static DirFilters makeDefaultDirFilters()
        {
            var ret = new DirFilters();
            // by default ignore some subdirectories by default
            ret.Add(".git");
            ret.Add(".vs");

            return ret;
        }

        static Regex makeFileFilter(string wildcardMask)
        {
            // hacky
            return new Regex(wildcardMask.Replace(".", "[.]").Replace("*", ".*").Replace("?", "."), RegexOptions.IgnoreCase);
        }

        static FileFilters makeDefaultFileFilters()
        {
            var ret = new FileFilters();
            ret.Add(makeFileFilter("*.sln"));
            return ret;
        }

        void OnFiltersChanged()
        {
            DirFilters newDirFilters;
            FileFilters newFileFilters;
            ReadFilters(out newDirFilters, out newFileFilters);

            bool haveNewFilters = false;
            if (newDirFilters == null)
            {
                haveNewFilters = _dirFilters != null;
            }
            else
            {
                if (_dirFilters != null)
                {
                    haveNewFilters = !_dirFilters.SetEquals(newDirFilters);
                }
                else
                {
                    haveNewFilters = true;
                }
            }

            if (haveNewFilters)
            {
                // so, there is probably a way to update the existing list based on the old and new
                // filters, but it's just too much work with too many potential bugs
                // we rely that this happens relatively rarely, so we just rebuild the entire tree
                _dirFilters = newDirFilters;
                BuildFullTree();
            }
        }

        private void Run()
        {
            ReadFilters(out _dirFilters, out _fileFilters);
            BuildFullTree();

            while (true)
            {
                var msg = _messageQueue.Take();
                switch (msg.type)
                {
                    case Msg.Type.Quit: return;
                    case Msg.Type.Created:
                        OnCreated(msg.path);
                        break;
                    case Msg.Type.Deleted:
                        OnDeleted(msg.path);
                        break;
                    case Msg.Type.Renamed:
                        OnRenamed(msg.oldPath, msg.path);
                        break;
                    case Msg.Type.FiltersChanged:
                        OnFiltersChanged();
                        break;
                }
            }
        }

        void OnCreated(string path)
        {
            if (Directory.Exists(path))
            {
                if (DirPassesFilter(path))
                {
                    BuildTree(path);
                }
            }
            else if (File.Exists(path) && FilePassesFilter(path))
            {
                lock (_allFiles)
                {
                    // we could potentially have the file already
                    // if the file was created quickly after the dir, we could have processed it
                    // in OnCrated(dir)
                    if (_allFiles.IndexOf(path) == -1)
                    {
                        _allFiles.Add(path);
                    }
                }
            }
            // else
            // something was created by the time we got here quickly deleted or renamed
            // just ignore this then
        }

        // only call when _allFiles is locked
        void _doRemoveDir(string path)
        {
            _allFiles.RemoveAll((string t) => {
                return t.Length > path.Length && t.StartsWith(path);
            });
        }

        void OnDeleted(string path)
        {
            // so, what was deleted?
            // there is no way to tell here wheter it's a dir or a file
            // so we'll check our list and if there is a single entry,
            // we'll assume it's a file. Otherwise it muse be a dir
            lock (_allFiles)
            {
                var index = _allFiles.IndexOf(path);
                if (index != -1)
                {
                    // file
                    _allFiles.RemoveAt(index);
                }
                else
                {
                    // dir but now we have the oportunity to check whether we have it
                    if (!DirPassesFilter(path)) return;
                    _doRemoveDir(path);
                }
            }
        }

        void OnRenamed(string oldPath, string newPath)
        {
            if (Directory.Exists(newPath))
            {
                // target is directory (we assume the source is also one)
                if (DirPassesFilter(newPath))
                {
                    bool entriesFound = false;
                    if (DirPassesFilter(oldPath))
                    {
                        // try going the fast lane
                        lock (_allFiles)
                        {
                            // is there really no more efficient way to do this in c#?
                            for (int i=0; i<_allFiles.Count; ++i)
                            {
                                string elem = _allFiles[i];
                                if (elem.Length > oldPath.Length && elem.StartsWith(oldPath))
                                {
                                    entriesFound = true;
                                    _allFiles[i] = newPath + elem.Substring(oldPath.Length);
                                }
                            }
                        }
                    }

                    if (!entriesFound)
                    {
                        // ok some directory was renamed and we have no entries for it
                        // 1. it could be that something was moved from an ignored directory
                        // to a non-ignored one
                        // 2. it could be that the directory is empty or that each individual
                        // file from inside doesn't pass the file filter but it could also be
                        // that the directory was quickly created and renamed and we ignored
                        // the creation because when we processed the message it was already
                        // renamed. So we must try treating it as a creation
                        BuildTree(newPath);
                    }
                }
                else
                {
                    // the target doesn't pass the filter
                    // we'll treat it as deletion
                    lock (_allFiles)
                    {
                        _doRemoveDir(oldPath);
                    }
                }
            }
            else if (File.Exists(newPath))
            {
                // target is file (we assume the source is also one)
                if (FilePassesFilter(newPath))
                {
                    if (FilePassesFilter(oldPath))
                    {
                        lock (_allFiles)
                        {
                            var index = _allFiles.IndexOf(oldPath);
                            if (index != -1)
                            {
                                // we found the file to rename
                                _allFiles[index] = newPath;
                                return; // job done
                            }
                        }
                    }

                    // since we're here we didn't find a file to rename
                    // either the file was moved from an ignored location
                    // or the file was quickly renamed to something and then to this
                    // but the first message we treated as deletion
                    lock (_allFiles)
                    {
                        _allFiles.Add(newPath);
                    }
                }
                else
                {
                    // the target doesn't pass the filter
                    // we'll treat it as deletion
                    if (!FilePassesFilter(newPath)) return; // we don't have it anyway

                    lock (_allFiles)
                    {
                        var index = _allFiles.IndexOf(newPath);
                        if (index == -1)
                        {
                            _allFiles.RemoveAt(index);
                        }
                    }
                }
            }
            else
            {
                // target is neither file nor dir
                // most likely something was renamed several times quickly
                // and by the time we get to handle this message the new name is in place
                // we have no choice but to clear our info for this entry
                OnDeleted(oldPath);
            }
        }

        private const string FiltersFilename = "VSOpenFileFromDirFilters.json";
        readonly private string _rootPath;
        private DirFilters _dirFilters = null;
        private FileFilters _fileFilters = null;
        private Thread _thread;
        private List<string> _allFiles;
        private BlockingCollection<Msg> _messageQueue;
        private FileSystemWatcher _watcher;
        private FileSystemWatcher _filtersWatcher;
    }
}
