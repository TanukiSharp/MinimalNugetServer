using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace MinimalNugetServer
{
    public class Version2RequestProcessor : RequestProcessorBase
    {
        private int downloadCounter;
        private int searchCounter;
        private int packagesCounter;
        private int findCounter;

        private static readonly PathString DownloadPrefix = new PathString("/download");

        public override PathString Segment { get; } = new PathString("/v2");

        public override async Task ProcessRequest(HttpContext context)
        {
            PathString path;

            if (context.Request.Path.StartsWithSegments(Segment, out path) == false)
                return;

            PathString downloadPath;

            if (path.StartsWithSegments(DownloadPrefix, out downloadPath))
            {
                Interlocked.Increment(ref downloadCounter);
                ReportCounters();
                await ProcessDownload(context, downloadPath.Value);
                return;
            }
            else if (path.Value == "/Search()")
            {
                Interlocked.Increment(ref searchCounter);
                ReportCounters();
                await ProcessSearch(context);
                return;
            }
            else if (path.Value.StartsWith("/Packages("))
            {
                Interlocked.Increment(ref packagesCounter);
                ReportCounters();
                await ProcessPackages(context);
                return;
            }
            else if (path.Value == "/FindPackagesById()")
            {
                Interlocked.Increment(ref findCounter);
                ReportCounters();
                await FindPackage(context);
                return;
            }
        }

        private void ReportCounters()
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine($"download: {downloadCounter}, search: {searchCounter}, packages: {packagesCounter}, find: {findCounter}");
        }

        private async Task ProcessDownload(HttpContext context, string downloadPath)
        {
            byte[] content = MasterData.Use(downloadPath, (packageInfo, contents, _downloadPath) =>
            {
                byte[] _content;
                if (contents.TryGetValue(_downloadPath.TrimStart(Characters.UrlPathSeparator), out _content))
                    return _content;
                return null;
            });

            if (content == null)
            {
                context.Response.StatusCode = 404;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Download: return {context.Response.StatusCode}");
                return;
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/octet-stream";
            await context.Response.Body.WriteAsync(content, 0, content.Length);
        }

        private Task ProcessSearch(HttpContext context)
        {
            return MasterData.Use(context, ProcessSearchSafe);
        }

        private async Task ProcessSearchSafe(IReadOnlyList<PackageInfo> packages, IReadOnlyDictionary<string, byte[]> contents, HttpContext context)
        {
            // targetFramework = '...'
            // $filter = IsLatestVersion

            IQueryCollection query = context.Request.Query;

            int skip = 0;
            int top = 0;

            bool check =
                int.TryParse(query.GetFirst("$skip", string.Empty), out skip) &&
                int.TryParse(query.GetFirst("$top", string.Empty), out top);

            if (check == false)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Search: return {context.Response.StatusCode}");
                return;
            }

            string searchTerm = query.GetFirst("searchTerm", "").Trim(Characters.SingleQuote);

            var packagesMatchingByIdIndices = new List<int>();
            for (int packageIndex = 0; packageIndex < packages.Count; packageIndex++)
            {
                if (packages[packageIndex].Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) > -1)
                    packagesMatchingByIdIndices.Add(packageIndex);
            }

            int totalPacketCount = packagesMatchingByIdIndices.Count;

            top = Math.Min(top, totalPacketCount);

            var matchingPackageIndices = packagesMatchingByIdIndices
                .Skip(skip)
                .Take(top);

            lock (Globals.ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine("--- Search -----------------------------------");
                foreach (var index in matchingPackageIndices)
                    Console.WriteLine($"{packages[index].Id} - {packages[index].LatestVersion}");
                Console.WriteLine("----------------------------------------------");
                Console.WriteLine();
            }

            var doc = new XElement(
                XmlElements.feed,
                new XAttribute(XmlElements.baze, XmlNamespaces.baze),
                new XAttribute(XmlElements.m, XmlNamespaces.m),
                new XAttribute(XmlElements.d, XmlNamespaces.d),
                new XAttribute(XmlElements.georss, XmlNamespaces.georss),
                new XAttribute(XmlElements.gml, XmlNamespaces.gml),
                new XElement(XmlElements.m_count, totalPacketCount.ToString()),
                matchingPackageIndices.Select(idx =>
                    new XElement(
                        XmlElements.entry,
                        new XElement(XmlElements.id, $"{context.Request.Scheme}://{context.Request.Host}/Packages(Id='{packages[idx].Id}',Version='{packages[idx].LatestVersion}')"),
                        new XElement(
                            XmlElements.content,
                            new XAttribute("type", "application/zip"),
                            new XAttribute("src", $"{context.Request.Scheme}://{context.Request.Host}{Segment}/download/{packages[idx].LatestContentId}")
                        ),
                        new XElement(
                            XmlElements.m_properties,
                            new XElement(XmlElements.d_id, packages[idx].Id),
                            new XElement(XmlElements.d_version, packages[idx].LatestVersion)
                        )
                    )
                )
            );

            var bytes = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task ProcessPackages(HttpContext context)
        {
            var path = Uri.UnescapeDataString(context.Request.Path.Value);

            int start = path.IndexOf('(');
            if (start == -1)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Packages: return {context.Response.StatusCode}");
                return;
            }

            start++;

            int end = path.IndexOf(')', start);
            if (end == -1)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Packages: return {context.Response.StatusCode}");
                return;
            }

            string id = null;
            Version version = null;

            var parts = path.Substring(start, end - start).Split(Characters.Coma);

            foreach (var part in parts)
            {
                var kv = part.Split(new[] { '=' }, 2);

                if (string.Equals(kv[0], "id", StringComparison.OrdinalIgnoreCase))
                    id = kv[1].Trim(Characters.SingleQuote);
                else if (string.Equals(kv[0], "version", StringComparison.OrdinalIgnoreCase))
                    Version.TryParse(kv[1].Trim(Characters.SingleQuote), out version);
            }

            if (string.IsNullOrWhiteSpace(id) || version == null)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Packages: return {context.Response.StatusCode}");
                return;
            }

            version = version.Normalize();

            string contentId = MasterData.FindContentId(id, version);

            if (contentId == null)
            {
                context.Response.StatusCode = 404;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Packages: return {context.Response.StatusCode}");
                return;
            }

            var doc = new XElement(XmlElements.entry,
                new XAttribute(XmlElements.baze, XmlNamespaces.baze),
                new XAttribute(XmlElements.m, XmlNamespaces.m),
                new XAttribute(XmlElements.d, XmlNamespaces.d),
                new XAttribute(XmlElements.georss, XmlNamespaces.georss),
                new XAttribute(XmlElements.gml, XmlNamespaces.gml),
                new XElement(XmlElements.id, $"{context.Request.Scheme}://{context.Request.Host}/Packages(Id='{id}',Version='{version}')"),
                new XElement(
                    XmlElements.content,
                    new XAttribute("type", "application/zip"),
                    new XAttribute("src", $"{context.Request.Scheme}://{context.Request.Host}{Segment}/download/{contentId}")
                ),
                new XElement(
                    XmlElements.m_properties,
                    new XElement(XmlElements.d_id, id),
                    new XElement(XmlElements.d_version, version)
                )
            );

            byte[] bytes = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task FindPackage(HttpContext context)
        {
            var strings = context.Request.Query["id"];
            if (strings.Count == 0)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Find: return {context.Response.StatusCode}");
                return;
            }

            var id = strings[0].Trim(Characters.SingleQuote);
            if (id.Length == 0)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Find: return {context.Response.StatusCode}");
                return;
            }

            VersionInfo[] versions = MasterData.Use(id, (packages, _, _id) =>
            {
                foreach (var packageInfo in packages)
                {
                    if (string.Equals(packageInfo.Id, _id, StringComparison.OrdinalIgnoreCase))
                        return packageInfo.Versions;
                }
                return null;
            });

            if (versions == null)
                versions = Globals.EmptyVersionInfoArray;
            else
            {
                lock (Globals.ConsoleLock)
                {
                    Console.WriteLine();
                    Console.WriteLine("--- Find -------------------------------------");
                    foreach (var v in versions)
                        Console.WriteLine($"{id} - {v.Version}");
                    Console.WriteLine("----------------------------------------------");
                    Console.WriteLine();
                }
            }

            var doc = new XElement(
                XmlElements.feed,
                new XAttribute(XmlElements.baze, XmlNamespaces.baze),
                new XAttribute(XmlElements.m, XmlNamespaces.m),
                new XAttribute(XmlElements.d, XmlNamespaces.d),
                new XAttribute(XmlElements.georss, XmlNamespaces.georss),
                new XAttribute(XmlElements.gml, XmlNamespaces.gml),
                new XElement(XmlElements.m_count, versions.Length.ToString()),
                versions.Select(x =>
                    new XElement(
                        XmlElements.entry,
                        new XElement(XmlElements.id, $"{context.Request.Scheme}://{context.Request.Host}{Segment}/Packages(Id='{id}',Version='{x.Version}')"),
                        new XElement(
                            XmlElements.content,
                            new XAttribute("type", "application/zip"),
                            new XAttribute("src", $"{context.Request.Scheme}://{context.Request.Host}{Segment}/download/{x.ContentId}")
                        ),
                        new XElement(
                            XmlElements.m_properties,
                            new XElement(XmlElements.d_id, id),
                            new XElement(XmlElements.d_version, x.Version)
                        )
                    )
                )
            );

            versions = null;

            var bytes = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/xml";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
