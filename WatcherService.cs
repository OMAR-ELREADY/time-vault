using System;
using System.Collections.Generic;
using System.IO;
using TimeVault.Core.DataStructures;
using TimeVault.Core.Models;

namespace TimeVault.Core.Services.Watcher
{
    public class WatcherService
    {
        // Keep track of the active watcher for each folder by its ID
        private Dictionary<string, FileSystemWatcher> activeWatchers = new Dictionary<string, FileSystemWatcher>();

        // Custom LinkedQueue to hold events until we process them
        private LinkedQueue eventQueue;

        // Remember when each file last changed so we can ignore rapid duplicates
        private Dictionary<string, DateTime> lastChangedTimes = new Dictionary<string, DateTime>();

        public WatcherService(LinkedQueue queue)
        {
            eventQueue = queue;
        }

        public void StartWatching(WatchedFolder folder)
        {
            if (string.IsNullOrEmpty(folder.Path) || !Directory.Exists(folder.Path))
                return;

            if (string.IsNullOrEmpty(folder.Id))
                return;

            // If we are already watching this folder, stop it first so we don't get duplicate watchers
            StopWatching(folder.Id);

            FileSystemWatcher watcher = new FileSystemWatcher(folder.Path)
            {
                IncludeSubdirectories = folder.IncludeSubdirectories
            };

            // Hook up the events
            watcher.Created += (sender, e) => RecordChange(folder.Id, e.FullPath, "Created");
            watcher.Changed += (sender, e) => RecordChange(folder.Id, e.FullPath, "Modified");
            watcher.Deleted += (sender, e) => RecordChange(folder.Id, e.FullPath, "Deleted");
            watcher.Renamed += (sender, e) => RecordChange(folder.Id, e.FullPath, "Renamed");

            watcher.EnableRaisingEvents = true;
            activeWatchers[folder.Id] = watcher;
        }

        public void StopWatching(string folderId)
        {
            if (activeWatchers.ContainsKey(folderId))
            {
                FileSystemWatcher watcher = activeWatchers[folderId];
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                activeWatchers.Remove(folderId);
            }
        }

        public void StopAll()
        {
            foreach (var watcher in activeWatchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            activeWatchers.Clear();
        }

        private void RecordChange(string folderId, string filePath, string changeType)
        {
            lock (lastChangedTimes)
            {
                DateTime now = DateTime.Now;

                // When an app saves a file, Windows fires multiple events within milliseconds.
                // We skip any event on the same file if it happened less than 500ms ago.
                if (lastChangedTimes.ContainsKey(filePath))
                {
                    double millisecondsSinceLast = (now - lastChangedTimes[filePath]).TotalMilliseconds;
                    if (millisecondsSinceLast < 500)
                    {
                        return;
                    }
                }

                lastChangedTimes[filePath] = now;

                // Create the change event object and push it into our queue
                ChangeEvent newEvent = new ChangeEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    WatchedFolderId = folderId,
                    FilePath = filePath,
                    ChangeType = changeType,
                    OccurredAt = now
                };

                eventQueue.Enqueue(newEvent);
            }
        }
    }
}
