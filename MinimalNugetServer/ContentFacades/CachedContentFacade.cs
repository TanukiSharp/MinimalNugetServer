using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MinimalNugetServer.ContentFacades
{
    public class CachedContentFacade : IContentFacadeAccessor
    {
        private readonly Dictionary<string, string> contentIds = new Dictionary<string, string>();
        private readonly MemoryCacheEntryOptions cacheEntryOptions;

        private IMemoryCache cache = new MemoryCache(DefaultMemoryCacheOptions.Default);

        /// <summary>
        /// Initializes the <see cref="CachedContentFacade"/> instance.
        /// </summary>
        /// <param name="cacheSlidingDuration">The sliding expiration delay of individual entries, in seconds.</param>
        public CachedContentFacade(uint cacheSlidingDuration)
        {
            cacheEntryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds((double)cacheSlidingDuration)
            };
        }

        public void Add(string contentId, string fullFilePath)
        {
            contentIds.Add(contentId, fullFilePath);
        }

        public void Clear()
        {
            contentIds.Clear();

            // this seems to be the simplest way to clear the cache :/
            cache.Dispose();
            cache = new MemoryCache(DefaultMemoryCacheOptions.Default);
        }

        public bool TryGetValue(string contentId, out byte[] content)
        {
            if (cache.TryGetValue(contentId, out content) == false)
            {
                try
                {
                    content = File.ReadAllBytes(contentIds[contentId]);
                    cache.Set(contentId, content, cacheEntryOptions);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private class DefaultMemoryCacheOptions : IOptions<MemoryCacheOptions>
        {
            public static readonly DefaultMemoryCacheOptions Default = new DefaultMemoryCacheOptions()
            {
                Value = new MemoryCacheOptions()
            };

            public MemoryCacheOptions Value { get; private set; }
        }
    }
}
