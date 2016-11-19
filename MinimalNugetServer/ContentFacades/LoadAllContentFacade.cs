using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MinimalNugetServer.ContentFacades
{
    public class LoadAllContentFacade : IContentFacadeAccessor
    {
        private readonly Dictionary<string, byte[]> contents = new Dictionary<string, byte[]>();

        public void Add(string contentId, string fullFilePath)
        {
            contents.Add(contentId, File.ReadAllBytes(fullFilePath));
        }

        public void Clear()
        {
            contents.Clear();
        }

        public bool TryGetValue(string contentId, out byte[] content)
        {
            return contents.TryGetValue(contentId, out content);
        }
    }
}
