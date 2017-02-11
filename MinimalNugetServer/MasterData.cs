using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Configuration;
using MinimalNugetServer.ContentFacades;

namespace MinimalNugetServer
{
    public enum CacheType
    {
        NoCacheLoadAll,
        Cache,
        NoCacheLoadNothing,
    }

    public struct CacheStrategy
    {
        public CacheType CacheType;
        public uint CacheEntryExpiration; // in seconds
    }

    public class MasterData
    {
        private IReadOnlyList<PackageInfo> packages;
        private readonly IContentFacadeAccessor contentFacade;

        private readonly string packagesPath;
        private readonly bool makeReadonly;

        private ReaderWriterLockSlim packagesProcessingLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private const int PackageProcessingThrottleDelay = 1000;
        private Timer packageProcessingThrottleTimer;

        public MasterData(IConfiguration nugetConfig, CacheStrategy cacheStrategy)
        {
            if (nugetConfig == null)
                throw new ArgumentException($"Invalid '{nameof(nugetConfig)}' argument.", nameof(nugetConfig));

            packagesPath = nugetConfig["packages"];
            makeReadonly = bool.Parse(nugetConfig["makeReadonly"]);

            if (cacheStrategy.CacheType == CacheType.NoCacheLoadAll)
                contentFacade = new LoadAllContentFacade();
            else if (cacheStrategy.CacheType == CacheType.NoCacheLoadNothing)
                contentFacade = new LoadNothingContentFacade();
            else if (cacheStrategy.CacheType == CacheType.Cache)
                contentFacade = new CachedContentFacade(cacheStrategy.CacheEntryExpiration);
            else
                throw new NotSupportedException($"Cache type '{cacheStrategy.CacheType}' not supported yet.");

            ProcessPackageFiles();
            WatchPackagesFolder();
        }

        public TResult Use<TResult, T1>(T1 arg1, Func<IReadOnlyList<PackageInfo>, IContentFacade, T1, TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            packagesProcessingLock.EnterReadLock();

            try
            {
                return action(packages, contentFacade, arg1);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        public TResult Use<TResult, T1, T2>(T1 arg1, T2 arg2, Func<IReadOnlyList<PackageInfo>,IContentFacade, T1, T2, TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            packagesProcessingLock.EnterReadLock();

            try
            {
                return action(packages, contentFacade, arg1, arg2);
            }
            finally
            {
                packagesProcessingLock.ExitReadLock();
            }
        }

        private readonly FileSystemWatcher watcher = new FileSystemWatcher();

        private void WatchPackagesFolder()
        {
            watcher.Path = packagesPath;
            watcher.NotifyFilter =
                NotifyFilters.Attributes |
                NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.Attributes |
                NotifyFilters.Size |
                NotifyFilters.LastWrite |
                NotifyFilters.CreationTime;
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
            watcher.EnableRaisingEvents = false;

            try
            {
                ProcessPackageFiles();
            }
            finally
            {
                watcher.EnableRaisingEvents = true;
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

            string[] allFiles = Directory.GetFiles(packagesPath, "*.nupkg", SearchOption.AllDirectories);

            if (makeReadonly && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
            {
                foreach (string filename in allFiles)
                    Utils.ChMod("555", filename);
            }

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

            contentFacade.Clear();
            foreach (var group in groups)
            {
                foreach (var info in group)
                    contentFacade.Add(info.ContentId, info.FullFilePath);
            }

            PackageInfo[] localPackages = (from g in groups
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
