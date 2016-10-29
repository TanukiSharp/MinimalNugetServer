using System;
using System.Collections.Generic;

namespace MinimalNugetServer
{
    public struct VersionInfo
    {
        public Version Version;
        public string ContentId;
    }

    public struct PackageInfo
    {
        public string Id;
        public Version LatestVersion;
        public string LatestContentId;
        public VersionInfo[] Versions;
    }

    public struct IdVersion
    {
        public string Id;
        public Version Version;
    }

    public struct Version3IndexInfo
    {
        public string IdSegment;
        public string Type;

        public Version3IndexInfo(string idSegment, string type)
        {
            IdSegment = idSegment;
            Type = type;
        }

        public IDictionary<string, string> ToSerializable(string urlPrefix)
        {
            return new Dictionary<string, string>
            {
                ["@id"] = $"{urlPrefix.TrimEnd(Characters.UrlPathSeparator)}/{IdSegment}",
                ["@type"] = Type,
            };
        }
    }
}
