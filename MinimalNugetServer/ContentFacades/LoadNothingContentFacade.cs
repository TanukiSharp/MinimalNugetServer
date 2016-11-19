using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MinimalNugetServer.ContentFacades
{
    public class LoadNothingContentFacade : IContentFacadeAccessor
    {
        private readonly Dictionary<string, string> contentIds = new Dictionary<string, string>();

        public void Add(string contentId, string fullFilePath)
        {
            contentIds.Add(contentId, fullFilePath);
        }

        public void Clear()
        {
            contentIds.Clear();
        }

        public bool TryGetValue(string contentId, out byte[] content)
        {
            string fullFilePath;

            if (contentIds.TryGetValue(contentId, out fullFilePath) == false)
            {
                content = null;
                return false;
            }

            try
            {
                content = File.ReadAllBytes(fullFilePath);
                return true;
            }
            catch
            {
                content = null;
                return false;
            }
        }
    }
}
