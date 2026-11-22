namespace TimeVault.Core.Services.VersionControl;

// Manages file versioning and snapshots
public class VersionService
{
    using System;
using System.IO;
using TimeVault.Core.Collections;
using TimeVault.Core.Models;

namespace TimeVault.Core.Services;

// Manages file versions and backup operations
public class VersionService
{
    private VersionList versionsList;
    private HashService hashService;
    private string backupDirectoryPath;

    // Initializes a new instance of VersionService with backup folder path
    public VersionService(string backupDirectoryPath)
    {
        this.versionsList = new VersionList();
        this.hashService = new HashService();
        this.backupDirectoryPath = backupDirectoryPath;
    }

    // Checks if a file has changed compared to its latest saved version
    public bool HasFileChanged(string filePath)
    {
        if (File.Exists(filePath) == false)
        {
            return false;
        }

        FileVersion? latestVersion = GetLatestVersion(filePath);

        if (latestVersion == null)
        {
            return true;
        }

        string currentHash = hashService.CalculateSha256(filePath);

        if (currentHash != latestVersion.Hash)
        {
            return true;
        }

        return false;
    }

    // Adds a new version for a file if it has changed
    public FileVersion? AddNewVersion(string filePath)
    {
        if (File.Exists(filePath) == false)
        {
            return null;
        }

        bool isChanged = HasFileChanged(filePath);

        if (isChanged == false)
        {
            return null;
        }

        string fileHash = hashService.CalculateSha256(filePath);
        FileInfo fileInfo = new FileInfo(filePath);
        long fileSizeBytes = fileInfo.Length;

        FileVersion? latestVersion = GetLatestVersion(filePath);
        int nextVersionNumber = 1;

        if (latestVersion != null)
        {
            nextVersionNumber = latestVersion.VersionNumber + 1;
        }

        FileVersion newVersion = new FileVersion();
        newVersion.Id = Guid.NewGuid().ToString();
        newVersion.FilePath = filePath;
        newVersion.Hash = fileHash;
        newVersion.SizeBytes = fileSizeBytes;
        newVersion.CreatedAt = DateTime.Now;
        newVersion.VersionNumber = nextVersionNumber;

        if (Directory.Exists(backupDirectoryPath) == false)
        {
            Directory.CreateDirectory(backupDirectoryPath);
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);
        string backupFileName = fileNameWithoutExtension + "_v" + nextVersionNumber + fileExtension;
        string destinationPath = Path.Combine(backupDirectoryPath, backupFileName);

        File.Copy(filePath, destinationPath, true);

        versionsList.Add(newVersion);

        return newVersion;
    }

    // Gets all versions for a specific file
    public FileVersion[] GetAllVersions(string filePath)
    {
        int matchCount = 0;
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    matchCount = matchCount + 1;
                }
            }

            current = current.Next;
        }

        FileVersion[] resultVersions = new FileVersion[matchCount];
        current = versionsList.Head;
        int arrayIndex = 0;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    resultVersions[arrayIndex] = current.Data;
                    arrayIndex = arrayIndex + 1;
                }
            }

            current = current.Next;
        }

        return resultVersions;
    }

    // Gets the latest version for a specific file
    public FileVersion? GetLatestVersion(string filePath)
    {
        FileVersion? latestVersion = null;
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    if (latestVersion == null)
                    {
                        latestVersion = current.Data;
                    }
                    else
                    {
                        if (current.Data.VersionNumber > latestVersion.VersionNumber)
                        {
                            latestVersion = current.Data;
                        }
                    }
                }
            }

            current = current.Next;
        }

        return latestVersion;
    }

    // Gets a specific version of a file by its version number
    public FileVersion? GetVersionByNumber(string filePath, int versionNumber)
    {
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    if (current.Data.VersionNumber == versionNumber)
                    {
                        return current.Data;
                    }
                }
            }

            current = current.Next;
        }

        return null;
    }

    // Deletes a specific version of a file by its version number
    public bool DeleteVersion(string filePath, int versionNumber)
    {
        FileVersion? targetVersion = GetVersionByNumber(filePath, versionNumber);

        if (targetVersion == null)
        {
            return false;
        }

        bool isRemoved = versionsList.Remove(targetVersion);

        if (isRemoved == true)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string fileExtension = Path.GetExtension(filePath);
            string backupFileName = fileNameWithoutExtension + "_v" + versionNumber + fileExtension;
            string backupFilePath = Path.Combine(backupDirectoryPath, backupFileName);

            if (File.Exists(backupFilePath) == true)
            {
                File.Delete(backupFilePath);
            }

            return true;
        }

        return false;
    }
}using System;
using System.IO;
using TimeVault.Core.Collections;
using TimeVault.Core.Models;

namespace TimeVault.Core.Services;

// Manages file versions and backup operations
public class VersionService
{
    private VersionList versionsList;
    private HashService hashService;
    private string backupDirectoryPath;

    // Initializes a new instance of VersionService with backup folder path
    public VersionService(string backupDirectoryPath)
    {
        this.versionsList = new VersionList();
        this.hashService = new HashService();
        this.backupDirectoryPath = backupDirectoryPath;
    }

    // Checks if a file has changed compared to its latest saved version
    public bool HasFileChanged(string filePath)
    {
        if (File.Exists(filePath) == false)
        {
            return false;
        }

        FileVersion? latestVersion = GetLatestVersion(filePath);

        if (latestVersion == null)
        {
            return true;
        }

        string currentHash = hashService.CalculateSha256(filePath);

        if (currentHash != latestVersion.Hash)
        {
            return true;
        }

        return false;
    }

    // Adds a new version for a file if it has changed
    public FileVersion? AddNewVersion(string filePath)
    {
        if (File.Exists(filePath) == false)
        {
            return null;
        }

        bool isChanged = HasFileChanged(filePath);

        if (isChanged == false)
        {
            return null;
        }

        string fileHash = hashService.CalculateSha256(filePath);
        FileInfo fileInfo = new FileInfo(filePath);
        long fileSizeBytes = fileInfo.Length;

        FileVersion? latestVersion = GetLatestVersion(filePath);
        int nextVersionNumber = 1;

        if (latestVersion != null)
        {
            nextVersionNumber = latestVersion.VersionNumber + 1;
        }

        FileVersion newVersion = new FileVersion();
        newVersion.Id = Guid.NewGuid().ToString();
        newVersion.FilePath = filePath;
        newVersion.Hash = fileHash;
        newVersion.SizeBytes = fileSizeBytes;
        newVersion.CreatedAt = DateTime.Now;
        newVersion.VersionNumber = nextVersionNumber;

        if (Directory.Exists(backupDirectoryPath) == false)
        {
            Directory.CreateDirectory(backupDirectoryPath);
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);
        string backupFileName = fileNameWithoutExtension + "_v" + nextVersionNumber + fileExtension;
        string destinationPath = Path.Combine(backupDirectoryPath, backupFileName);

        File.Copy(filePath, destinationPath, true);

        versionsList.Add(newVersion);

        return newVersion;
    }

    // Gets all versions for a specific file
    public FileVersion[] GetAllVersions(string filePath)
    {
        int matchCount = 0;
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    matchCount = matchCount + 1;
                }
            }

            current = current.Next;
        }

        FileVersion[] resultVersions = new FileVersion[matchCount];
        current = versionsList.Head;
        int arrayIndex = 0;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    resultVersions[arrayIndex] = current.Data;
                    arrayIndex = arrayIndex + 1;
                }
            }

            current = current.Next;
        }

        return resultVersions;
    }

    // Gets the latest version for a specific file
    public FileVersion? GetLatestVersion(string filePath)
    {
        FileVersion? latestVersion = null;
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    if (latestVersion == null)
                    {
                        latestVersion = current.Data;
                    }
                    else
                    {
                        if (current.Data.VersionNumber > latestVersion.VersionNumber)
                        {
                            latestVersion = current.Data;
                        }
                    }
                }
            }

            current = current.Next;
        }

        return latestVersion;
    }

    // Gets a specific version of a file by its version number
    public FileVersion? GetVersionByNumber(string filePath, int versionNumber)
    {
        VersionNode? current = versionsList.Head;

        while (current != null)
        {
            if (current.Data != null)
            {
                if (current.Data.FilePath == filePath)
                {
                    if (current.Data.VersionNumber == versionNumber)
                    {
                        return current.Data;
                    }
                }
            }

            current = current.Next;
        }

        return null;
    }

    // Deletes a specific version of a file by its version number
    public bool DeleteVersion(string filePath, int versionNumber)
    {
        FileVersion? targetVersion = GetVersionByNumber(filePath, versionNumber);

        if (targetVersion == null)
        {
            return false;
        }

        bool isRemoved = versionsList.Remove(targetVersion);

        if (isRemoved == true)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string fileExtension = Path.GetExtension(filePath);
            string backupFileName = fileNameWithoutExtension + "_v" + versionNumber + fileExtension;
            string backupFilePath = Path.Combine(backupDirectoryPath, backupFileName);

            if (File.Exists(backupFilePath) == true)
            {
                File.Delete(backupFilePath);
            }

            return true;
        }

        return false;
    }
}
}