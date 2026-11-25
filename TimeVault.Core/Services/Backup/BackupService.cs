namespace TimeVault.Core.Services.Backup;

using System;
using System.Collections.Generic;
using System.IO;
using TimeVault.Core.Models;
using TimeVault.Core.Services.Hashing;

// Executes backup operations for watched folders and scheduled jobs
public class BackupService
{
    private readonly HashService _hashService;

    public BackupService(HashService? hashService = null)
    {
        _hashService = hashService ?? new HashService();
    }

    /// <summary>
    /// Executes a scheduled backup job for a watched folder.
    /// Updates the job's LastRunAt timestamp on success or LastError on failure.
    /// </summary>
    public bool ExecuteJob(BackupJob job, WatchedFolder folder, string backupRootDirectory)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));
        if (folder == null) throw new ArgumentNullException(nameof(folder));
        if (string.IsNullOrWhiteSpace(backupRootDirectory))
            throw new ArgumentException("Backup root directory cannot be empty.", nameof(backupRootDirectory));

        if (!job.IsEnabled)
            return false;

        try
        {
            var snapshot = BackupFolder(folder, backupRootDirectory, $"Job-{job.Id}-{DateTime.Now:yyyyMMdd_HHmmss}");

            job.LastRunAt = DateTime.Now;
            job.LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            job.LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Backs up all matching files in a watched folder into a snapshot directory.
    /// Returns the Snapshot record with file-to-version mappings.
    /// </summary>
    public Snapshot BackupFolder(WatchedFolder folder, string backupRootDirectory, string? snapshotName = null)
    {
        if (folder == null) throw new ArgumentNullException(nameof(folder));
        if (string.IsNullOrEmpty(folder.Path) || !Directory.Exists(folder.Path))
        {
            throw new DirectoryNotFoundException($"Watched folder path not found: {folder.Path}");
        }

        string folderId = folder.Id ?? "default";
        string snapshotId = $"snap-{Guid.NewGuid().ToString("N")[..8]}";
        string targetDir = Path.Combine(backupRootDirectory, folderId, snapshotId);

        Directory.CreateDirectory(targetDir);

        var searchOption = folder.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var allFiles = Directory.GetFiles(folder.Path, "*.*", searchOption);
        var versionMappings = new List<string>();

        foreach (var file in allFiles)
        {
            if (!MatchesFilter(file, folder.FileFilters))
                continue;

            // Preserve relative folder hierarchy in the backup destination
            string relativePath = Path.GetRelativePath(folder.Path, file);
            string destinationFile = Path.Combine(targetDir, relativePath);

            string? destinationFolder = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            File.Copy(file, destinationFile, overwrite: true);

            string versionId = $"ver-{Guid.NewGuid().ToString("N")[..8]}";
            versionMappings.Add($"{file}={versionId}");
        }

        var snapshot = new Snapshot
        {
            Id = snapshotId,
            Name = snapshotName ?? $"Snapshot {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            CreatedAt = DateTime.Now,
            WatchedFolderId = folder.Id,
            FileVersions = string.Join(";", versionMappings)
        };

        return snapshot;
    }

    /// <summary>
    /// Backs up a single file to a destination folder, returning a FileVersion record.
    /// </summary>
    public FileVersion BackupFile(string sourceFilePath, string targetDirectory, int versionNumber = 1)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path cannot be empty.", nameof(sourceFilePath));

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file does not exist.", sourceFilePath);

        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Target directory cannot be empty.", nameof(targetDirectory));

        Directory.CreateDirectory(targetDirectory);

        string fileName = Path.GetFileName(sourceFilePath);
        string versionId = $"ver-{Guid.NewGuid().ToString("N")[..8]}";
        string destinationPath = Path.Combine(targetDirectory, $"{versionId}_{fileName}");

        File.Copy(sourceFilePath, destinationPath, overwrite: true);

        string hash = _hashService.ComputeHash(sourceFilePath);
        long size = new FileInfo(sourceFilePath).Length;

        return new FileVersion
        {
            Id = versionId,
            FilePath = sourceFilePath,
            Hash = hash,
            SizeBytes = size,
            CreatedAt = DateTime.Now,
            VersionNumber = versionNumber
        };
    }

    /// <summary>
    /// Restores a backed up file to a target destination path.
    /// </summary>
    public bool RestoreFile(string backupFilePath, string destinationFilePath, bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file does not exist.", backupFilePath);

        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationFilePath));

        string? destDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        File.Copy(backupFilePath, destinationFilePath, overwrite);
        return true;
    }

    /// <summary>
    /// Verifies whether the backup copy matches the original source file hash.
    /// </summary>
    public bool VerifyBackup(string originalFilePath, string backupFilePath)
    {
        if (!File.Exists(originalFilePath) || !File.Exists(backupFilePath))
            return false;

        string originalHash = _hashService.ComputeHash(originalFilePath);
        string backupHash = _hashService.ComputeHash(backupFilePath);

        return _hashService.AreEqual(originalHash, backupHash);
    }

    /// <summary>
    /// Checks if a file path matches the comma-separated filters (e.g. ".txt,.cs").
    /// An empty filter allows all files.
    /// </summary>
    private bool MatchesFilter(string filePath, string? fileFilters)
    {
        if (string.IsNullOrWhiteSpace(fileFilters))
            return true;

        string extension = Path.GetExtension(filePath);
        var filters = fileFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var filter in filters)
        {
            string f = filter.StartsWith(".") ? filter : "." + filter;
            if (string.Equals(extension, f, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
