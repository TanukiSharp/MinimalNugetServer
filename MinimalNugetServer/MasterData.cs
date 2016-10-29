using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace MinimalNugetServer
{
    public class MasterData
    {
        private IReadOnlyList<PackageInfo> packages;
        private IReadOnlyDictionary<string, byte[]> contents;

        private readonly string packagesPath;

        private ReaderWriterLockSlim packagesProcessingLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private const int PackageProcessingThrottleDelay = 1000;
        private Timer packageProcessingThrottleTimer;

        public MasterData(string packagesPath)
        {
            if (string.IsNullOrWhiteSpace(packagesPath))
                throw new ArgumentException($"Invalid '{nameof(packagesPath)}' argument.", nameof(packagesPath));

            this.packagesPath = packagesPath;

            ProcessPackageFiles();
            WatchPackagesFolder();
        }

        public TResult Use<TResult, T1>(T1 arg1, Func<IReadOnlyList<PackageInfo>, IReadOnlyDictionary<string, byte[]>, T1, TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            packagesProcessingLock.EnterReadLock();

            try
            {
                return action(packages, contents, arg1);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        public TResult Use<TResult, T1, T2>(T1 arg1, T2 arg2, Func<IReadOnlyList<PackageInfo>, IReadOnlyDictionary<string, byte[]>, T1, T2, TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            packagesProcessingLock.EnterReadLock();

            try
            {
                return action(packages, contents, arg1, arg2);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        private void WatchPackagesFolder()
        {
            var watcher = new FileSystemWatcher();

            watcher.Path = packagesPath;
            watcher.Filter = "*.nupkg";
            watcher.Changed += OnPackagesFolderChanged;
            watcher.Created += OnPackagesFolderChanged;
            watcher.Deleted += OnPackagesFolderChanged;
            watcher.Renamed += OnPackagesFolderChanged;
            watcher.Error += OnError;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            packageProcessingThrottleTimer = new Timer(OnPackageProcessingThrottleTimeout, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnPackageProcessingThrottleTimeout(object ignore)
        {
            packagesProcessingLock.EnterWriteLock();

            try
            {
                ProcessPackageFiles();
            }
            finally
            {
                packagesProcessingLock.ExitWriteLock();
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine($"OnError [{e.GetException().Message}]");
        }

        private void OnPackagesFolderChanged(object sender, FileSystemEventArgs e)
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine($"Restarting package processing throttle timer");

            packageProcessingThrottleTimer.Change(PackageProcessingThrottleDelay, Timeout.Infinite);
        }

        private void ProcessPackageFiles()
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine("Processing packages...");

            var allFiles = Directory.GetFiles(packagesPath, "*.nupkg", SearchOption.AllDirectories);

            var groups = from fullFilePath in allFiles
                         where fullFilePath.EndsWith(".symbols.nupkg") == false
                         where File.Exists(fullFilePath)
                         let idVersion = Utils.SplitIdAndVersion(Path.GetFileNameWithoutExtension(fullFilePath))
                         select new
                         {
                             Id = idVersion.Id,
                             Version = idVersion.Version,
                             FullFilePath = fullFilePath,
                             ContentId = ((uint)fullFilePath.GetHashCode()).ToString(),
                             Content = File.ReadAllBytes(fullFilePath)
                         }
                         into x
                         group x by x.Id;

            var localContents = groups
                .SelectMany(x => x)
                .ToDictionary(x => x.ContentId, x => x.Content);

            var localPackages = (from g in groups
                        let versions = g.Select(x => new VersionInfo { Version = x.Version, ContentId = x.ContentId }).OrderBy(x => x.Version).ToArray()
                        select new PackageInfo
                        {
                            Id = g.Key,
                            Versions = versions,
                            LatestVersion = versions[versions.Length - 1].Version,
                            LatestContentId = versions[versions.Length - 1].ContentId
                        })
                        .OrderBy(x => x.Id)
                        .ToArray();

            packages = new ReadOnlyCollection<PackageInfo>(localPackages);
            contents = new ReadOnlyDictionary<string, byte[]>(localContents);

            lock (Globals.ConsoleLock)
            {
                Console.WriteLine("Processing packages done!");
                Console.WriteLine();
            }
        }

        public int FindPackageIndex(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return -1;

            packagesProcessingLock.EnterReadLock();

            try
            {
                return FindPackageIndexNotSafe(packageId);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        private int FindPackageIndexNotSafe(string packageId)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                if (string.Equals(packages[i].Id, packageId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public string FindContentId(string packageId, Version version)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            packagesProcessingLock.EnterReadLock();

            try
            {
                return FindContentIdNotSafe(packageId, version);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        private string FindContentIdNotSafe(string packageId, Version version)
        {
            for (int i = 0; i < packages.Count; i++)
            {
                if (string.Equals(packages[i].Id, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    VersionInfo[] versions = packages[i].Versions;
                    for (int j = 0; j < versions.Length; j++)
                    {
                        if (versions[j].Version == version)
                            return versions[j].ContentId;
                    }
                }
            }

            return null;
        }
    }
}
