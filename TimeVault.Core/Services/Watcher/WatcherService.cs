using System;
using System.Collections.Generic;
using System.IO;
using TimeVault.Core.DataStructures;
using TimeVault.Core.Models;

namespace TimeVault.Core.Services.Watcher
{
    // Monitors file system changes in watched folders.
    // Produces ChangeEvent objects into a LinkedQueue for VersionService to consume.
    public class WatcherService
    {
        // String constants for change types (match our ChangeEvent.ChangeType string property).
        private const string Created = "Created";
        private const string Modified = "Modified";
        private const string Deleted = "Deleted";
        private const string Renamed = "Renamed";

        // Maps folderId -> FileSystemWatcher. Allows starting/stopping per folder.
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();

        // Reverse lookup: FileSystemWatcher -> folderId. Needed in event handlers.
        private readonly Dictionary<FileSystemWatcher, string> _watcherToFolderId = new();

        // Our custom queue for pending change events (FIFO).
        private readonly LinkedQueue _eventQueue;

        // Debounce: maps "ChangeType|filePath" -> last event time.
        // Prevents duplicate events when apps save files (multiple rapid events).
        private readonly Dictionary<string, DateTime> _lastEventTime = new();

        // Lock object for thread-safe access to _lastEventTime.
        private readonly object _lock = new();

        // Debounce window in milliseconds (500ms = ignore rapid duplicates).
        private const int DebounceMs = 500;

        // Constructor: inject the shared event queue.
        public WatcherService(LinkedQueue eventQueue)
        {
            _eventQueue = eventQueue;
        }

        // Starts watching a folder. Throws if folder is invalid.
        public void StartWatching(WatchedFolder folder)
        {
            if (folder == null)
                throw new ArgumentNullException(nameof(folder));

            if (string.IsNullOrWhiteSpace(folder.Id))
                throw new ArgumentException("Folder Id is required.");

            if (string.IsNullOrWhiteSpace(folder.Path))
                throw new ArgumentException("Folder Path is required.");

            if (!Directory.Exists(folder.Path))
                throw new DirectoryNotFoundException($"Folder not found: {folder.Path}");

            // If already watching this folder, stop it first.
            StopWatching(folder.Id);

            // Create and configure the watcher.
            var watcher = new FileSystemWatcher(folder.Path)
            {
                IncludeSubdirectories = folder.IncludeSubdirectories,
                // Only watch for file name changes, last write time, and size.
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            // Hook up event handlers (explicit methods, not lambdas).
            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;

            // Start watching.
            watcher.EnableRaisingEvents = true;

            // Store for later lookup.
            _watchers[folder.Id] = watcher;
            _watcherToFolderId[watcher] = folder.Id;
        }

        // Stops watching a specific folder by its ID.
        public void StopWatching(string folderId)
        {
            if (string.IsNullOrWhiteSpace(folderId))
                return;

            if (_watchers.TryGetValue(folderId, out var watcher))
            {
                // Unsubscribe from events before disposing.
                watcher.Created -= OnCreated;
                watcher.Changed -= OnChanged;
                watcher.Deleted -= OnDeleted;
                watcher.Renamed -= OnRenamed;

                watcher.EnableRaisingEvents = false;
                watcher.Dispose();

                _watcherToFolderId.Remove(watcher);
                _watchers.Remove(folderId);
            }
        }

        // Stops all watchers and cleans up.
        public void StopAll()
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            _watcherToFolderId.Clear();
        }

        // Event handler for file created.
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            HandleEvent(sender, e, Created);
        }

        // Event handler for file changed.
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            HandleEvent(sender, e, Modified);
        }

        // Event handler for file deleted.
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            HandleEvent(sender, e, Deleted);
        }

        // Event handler for file renamed.
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            HandleEvent(sender, e, Renamed);
        }

        // Central handler: debounce, create ChangeEvent, enqueue.
        private void HandleEvent(object sender, FileSystemEventArgs e, string type)
        {
            // Sender must be a FileSystemWatcher we created.
            if (sender is not FileSystemWatcher watcher)
                return;

            // Find which folder this watcher belongs to.
            if (!_watcherToFolderId.TryGetValue(watcher, out var folderId))
                return;

            // Ignore directory events (we only care about files).
            if (type != Deleted && Directory.Exists(e.FullPath))
                return;

            // Debounce key includes type so Created and Modified on same file are separate.
            var key = type + "|" + e.FullPath;
            var now = DateTime.Now;

            lock (_lock)
            {
                // Check if we saw this same type+path recently.
                if (_lastEventTime.TryGetValue(key, out var lastTime))
                {
                    var elapsedMs = (now - lastTime).TotalMilliseconds;
                    if (elapsedMs < DebounceMs)
                    {
                        // Too soon since last event - skip it.
                        return;
                    }
                }

                // Update last event time for this key.
                _lastEventTime[key] = now;

                // Create the change event.
                var changeEvent = new ChangeEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    WatchedFolderId = folderId,
                    FilePath = e.FullPath,
                    ChangeType = type,
                    OccurredAt = now
                };

                // Add to queue for VersionService to process.
                _eventQueue.Enqueue(changeEvent);
            }
        }
    }
}