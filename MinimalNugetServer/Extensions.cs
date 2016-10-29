using System;
using Microsoft.AspNetCore.Http;

namespace MinimalNugetServer
{
    public static class VersionExtensions
    {
        public static Version Normalize(this Version version)
        {
            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build),
                Math.Max(0, version.Revision)
            );
        }
    }

    public static class QueryCollectionExtensions
    {
        public static string GetFirst(this IQueryCollection query, string key, string defaultValue)
        {
            var values = query[key];

            if (values.Count == 0)
                return defaultValue;

            return values[0];
        }
    }
}
