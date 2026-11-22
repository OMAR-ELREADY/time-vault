namespace TimeVault.Core.Services.Watcher;

using System;
using System.Collections.Generic;
using System.IO;
using TimeVault.Core.DataStructures;
using TimeVault.Core.Models;

// Monitors file system changes in watched folders
public class WatcherService
{
    private readonly Dictionary<string, FileSystemWatcher> _activeWatchers = new();
    private readonly LinkedQueue _eventQueue;
    private readonly Dictionary<string, DateTime> _lastChangedTimes = new();

    public WatcherService(LinkedQueue queue)
    {
        _eventQueue = queue;
    }

    public void StartWatching(WatchedFolder folder)
    {
        if (string.IsNullOrEmpty(folder.Path) || !Directory.Exists(folder.Path))
            return;

        if (string.IsNullOrEmpty(folder.Id))
            return;

        StopWatching(folder.Id);

        var watcher = new FileSystemWatcher(folder.Path)
        {
            IncludeSubdirectories = folder.IncludeSubdirectories
        };

        watcher.Created += (sender, e) => RecordChange(folder.Id, e.FullPath, "Created");
        watcher.Changed += (sender, e) => RecordChange(folder.Id, e.FullPath, "Modified");
        watcher.Deleted += (sender, e) => RecordChange(folder.Id, e.FullPath, "Deleted");
        watcher.Renamed += (sender, e) => RecordChange(folder.Id, e.FullPath, "Renamed");

        watcher.EnableRaisingEvents = true;
        _activeWatchers[folder.Id] = watcher;
    }

    public void StopWatching(string folderId)
    {
        if (_activeWatchers.TryGetValue(folderId, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _activeWatchers.Remove(folderId);
        }
    }

    public void StopAll()
    {
        foreach (var watcher in _activeWatchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _activeWatchers.Clear();
    }

    private void RecordChange(string folderId, string filePath, string changeType)
    {
        lock (_lastChangedTimes)
        {
            DateTime now = DateTime.Now;

            if (_lastChangedTimes.TryGetValue(filePath, out var lastTime))
            {
                if ((now - lastTime).TotalMilliseconds < 500)
                {
                    return;
                }
            }

            _lastChangedTimes[filePath] = now;

            var newEvent = new ChangeEvent
            {
                Id = Guid.NewGuid().ToString(),
                WatchedFolderId = folderId,
                FilePath = filePath,
                ChangeType = changeType,
                OccurredAt = now
            };

            _eventQueue.Enqueue(newEvent);
        }
    }
}
