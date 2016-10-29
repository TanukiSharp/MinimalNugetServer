using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace MinimalNugetServer
{
    public class Version3RequestProcessor : RequestProcessorBase
    {
        public override PathString Segment { get; } = new PathString("/v3");

        public override async Task ProcessRequest(HttpContext context)
        {
            PathString path;

            if (context.Request.Path.StartsWithSegments(Segment, out path) == false)
                return;

            PathString containerPath;

            string rootPath = path.Value.TrimStart(Characters.UrlPathSeparator).ToLower();
            if (rootPath == string.Empty || rootPath == "index.json")
            {
                await ProcessRoot(context);
                return;
            }
            else if (path.StartsWithSegments("/container", out containerPath))
            {
                await ProcessContainer(context, containerPath);
                return;
            }
        }

        private static readonly Version3IndexInfo[] indexIds = new Version3IndexInfo[]
        {
            new Version3IndexInfo("registration", "RegistrationsBaseUrl"),
            new Version3IndexInfo("container", "PackageBaseAddress/3.0.0"),
            new Version3IndexInfo("query", "SearchQueryService"),
            new Version3IndexInfo("autocomplete", "SearchAutocompleteService"),
            new Version3IndexInfo("catalog/index.json", "Catalog/3.0.0"),
        };

        private async Task ProcessRoot(HttpContext context)
        {
            var urlPrefix = $"{context.Request.Scheme}://{context.Request.Host}{Segment}";

            string json = JsonConvert.SerializeObject(new
            {
                version = "3.0.0",
                resources = indexIds
                    .Select(x => x.ToSerializable(urlPrefix))
                    .ToArray()
            }, Formatting.None);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, Encoding.UTF8);

            lock (Globals.ConsoleLock)
                Console.WriteLine($"Serving index");
        }

        private async Task ProcessContainer(HttpContext context, PathString remainingPath)
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine($"{nameof(remainingPath)}: {remainingPath}");

            string[] pathParts = remainingPath.Value.Split(Characters.UrlPathSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 2)
            {
                context.Response.StatusCode = 400;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Container: return {context.Response.StatusCode}");
                return;
            }

            string id = pathParts[0];

            lock (Globals.ConsoleLock)
                Console.WriteLine($"{nameof(id)}: {id}");

            int packageIndex = MasterData.FindPackageIndex(id);

            if (packageIndex < 0)
            {
                context.Response.StatusCode = 404;
                lock (Globals.ConsoleLock)
                    Console.WriteLine($"Container: return {context.Response.StatusCode}");
                return;
            }

            if (string.Equals(pathParts[1], "index.json", StringComparison.OrdinalIgnoreCase))
                await ProcessContainerIndex(context, packageIndex);
            else
            {
                Version version;
                if (Version.TryParse(pathParts[1], out version))
                    await ProcessContainerVersion(context, packageIndex, version.Normalize());
            }
        }

        private Task ProcessContainerIndex(HttpContext context, int packageIndex)
        {
            string[] allVersions = MasterData.Use(packageIndex, (packages, _, _packageIndex) =>
            {
                return packages[_packageIndex].Versions
                    .Select(x => x.Version.ToString())
                    .ToArray();
            });

            string json = JsonConvert.SerializeObject(new { versions = allVersions }, Formatting.None);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(json, Encoding.UTF8);
        }

        private async Task ProcessContainerVersion(HttpContext context, int packageIndex, Version version)
        {
            lock (Globals.ConsoleLock)
                Console.WriteLine($"ProcessContainerVersion [version: {version}]");

            byte[] content = MasterData.Use(packageIndex, version, (packages, contents, _packageIndex, _version) =>
            {
                VersionInfo[] versions = packages[_packageIndex].Versions;

                for (int i = 0; i < versions.Length; i++)
                {
                    if (versions[i].Version == _version)
                        return contents[versions[i].ContentId];
                }

                return null;
            });

            if (content != null)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/octet-stream";
                await context.Response.Body.WriteAsync(content, 0, content.Length);
                return;
            }

            context.Response.StatusCode = 404;
            lock (Globals.ConsoleLock)
                Console.WriteLine($"ContainerVersion: return {context.Response.StatusCode}");
        }
    }
}
