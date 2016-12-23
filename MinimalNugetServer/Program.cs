using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MinimalNugetServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string configurationFile = "configuration.json";

            if (args.Length == 1)
                configurationFile = args[0];

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configurationFile, optional: false);

            IConfigurationRoot configuration = builder.Build();

            var program = new Program(configuration["nuget:packages"], LoadCacheStrategy(configuration));

            program.Run(configuration["server:url"]);
        }

        private static CacheStrategy LoadCacheStrategy(IConfigurationRoot config)
        {
            CacheType cacheType = CacheType.NoCacheLoadAll;
            uint cacheDuration = 24 * 3600; // 24 hours

            string strCacheType = config["cache:type"];
            CacheType tempCacheType;
            if (Enum.TryParse(strCacheType, true, out tempCacheType))
                cacheType = tempCacheType;

            string strCacheDuration = config["cache:duration"];
            uint tempCacheDuration;
            if (uint.TryParse(strCacheDuration, out tempCacheDuration))
                cacheDuration = tempCacheDuration;

            lock (Globals.ConsoleLock)
            {
                Console.Write($"Cache strategy: {cacheType}");
                if (cacheType == CacheType.Cache)
                    Console.Write($" ({cacheDuration} seconds)");
                Console.WriteLine();
            }

            return new CacheStrategy
            {
                CacheType = cacheType,
                CacheEntryExpiration = cacheDuration,
            };
        }

        // ===================================================================================

        private readonly MasterData masterData;

        private readonly RequestProcessorBase[] requestProcessors = new RequestProcessorBase[]
        {
            new Version2RequestProcessor(),
            new Version3RequestProcessor()
        };

        public Program(string packagesPath, CacheStrategy cacheStrategy)
        {
            masterData = new MasterData(packagesPath, cacheStrategy);
        }

        private void Run(string url)
        {
            foreach (var requestProcessor in requestProcessors)
                requestProcessor.Initialize(masterData);

            lock (Globals.ConsoleLock)
            {
                Console.WriteLine($"Build {GitCommitHash.Value}");
                Console.WriteLine();
            }

            new WebHostBuilder()
                .UseKestrel()
                .UseUrls(url)
                .Configure(app => app.Run(OnRequest))
                .Build()
                .Run();
        }

        private async Task OnRequest(HttpContext context)
        {
            lock (Globals.ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine("---");
                Console.WriteLine($"{nameof(context.Request.ContentLength)}: {context.Request.ContentLength}");
                Console.WriteLine($"{nameof(context.Request.ContentType)}: {context.Request.ContentType}");
                Console.WriteLine($"{nameof(context.Request.Method)}: {context.Request.Method}");
                Console.WriteLine($"{nameof(context.Request.Path)}: {context.Request.Path}");
                Console.WriteLine($"{nameof(context.Request.PathBase)}: {context.Request.PathBase}");
                Console.WriteLine($"{nameof(context.Request.Protocol)}: {context.Request.Protocol}");
                Console.WriteLine($"{nameof(context.Request.QueryString)}: {context.Request.QueryString}");
                Console.WriteLine("---");
                Console.WriteLine();
            }

            foreach (var requestProcessor in requestProcessors)
            {
                if (context.Request.Path.StartsWithSegments(requestProcessor.Segment))
                {
                    await requestProcessor.ProcessRequest(context);
                    return;
                }
            }

            lock (Globals.ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unprocessed request");
                Console.ResetColor();
            }

            context.Response.StatusCode = 404;
        }
    }
}
