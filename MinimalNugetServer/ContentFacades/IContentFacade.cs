using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinimalNugetServer.ContentFacades
{
    public interface IContentFacade
    {
        bool TryGetValue(string contentId, out byte[] content);
    }

    public interface IContentFacadeAccessor : IContentFacade
    {
        void Add(string contentId, string fullFilePath);
        void Clear();
    }
}
