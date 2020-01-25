using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace watcher
{
    class Worker
    {
        public struct Msg
        {
            public enum Type
            {
                Created,
                Deleted,
                Renamed,
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

            public static Msg Quit()
            {
                Msg msg;
                msg.type = Type.Quit;
                msg.path = null;
                msg.oldPath = null;
                return msg;
            }
        }

        readonly private string _rootPath;

        public Worker(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            _rootPath = path;
            _messageQueue = new BlockingCollection<Msg>();
            _allFiles = new List<string>();

            // star the watcher before we start the thread and collect file infos
            // thus if any events happen while we're building the list, we'll have
            // the opportunity to reflect them
            _watcher = new FileSystemWatcher();
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Path = _rootPath;
            _watcher.Created += (object source, FileSystemEventArgs e) => SendMessage(Msg.Created(e.FullPath));
            _watcher.Deleted += (object source, FileSystemEventArgs e) => SendMessage(Msg.Deleted(e.FullPath));
            _watcher.Renamed += (object source, RenamedEventArgs e) => SendMessage(Msg.Renamed(e.OldFullPath, e.FullPath));
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;

            _thread = new Thread(new ThreadStart(this.Run));
            _thread.Start();
        }

        public void SendMessage(Msg msg) => _messageQueue.Add(msg);
        public void Join()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher = null;
            SendMessage(Msg.Quit());
            _thread.Join();
            _thread = null;
        }

        private bool DirPassesFilter(string dir)
        {
            return true;
        }

        private bool FilePassesFilter(string file)
        {
            return true;
        }

        private List<string> _allFiles;

        public string[] GetFiles()
        {
            lock (_allFiles)
            {
                return _allFiles.ToArray();
            }
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

        private void Run()
        {
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

        private Thread _thread;
        private BlockingCollection<Msg> _messageQueue;
        private FileSystemWatcher _watcher = null;
    }
}