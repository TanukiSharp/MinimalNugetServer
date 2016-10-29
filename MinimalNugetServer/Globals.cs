using System;
using System.Xml.Linq;

namespace MinimalNugetServer
{
    public static class Globals
    {
        public static readonly object ConsoleLock = new object();
        public static readonly VersionInfo[] EmptyVersionInfoArray = new VersionInfo[0];
    }

    public static class Characters
    {
        public static readonly char[] UrlPathSeparator = new char[] { '/' };
        public static readonly char[] SingleQuote = new char[] { '\'' };
        public static readonly char[] Coma = new char[] { ',' };
        public static readonly char[] Dot = new char[] { '.' };
    }

    public static class XmlNamespaces
    {
        public static readonly XNamespace xmlns = "http://www.w3.org/2005/Atom";
        public static readonly XNamespace baze = "https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet";
        public static readonly XNamespace m = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        public static readonly XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        public static readonly XNamespace georss = "http://www.georss.org/georss";
        public static readonly XNamespace gml = "http://www.opengis.net/gml";
    }

    public static class XmlElements
    {
        public static readonly XName feed = XmlNamespaces.xmlns + "feed";
        public static readonly XName entry = XmlNamespaces.xmlns + "entry";
        public static readonly XName id = XmlNamespaces.xmlns + "id";
        public static readonly XName content = XmlNamespaces.xmlns + "content";

        public static readonly XName m_count = XmlNamespaces.m + "count";
        public static readonly XName m_properties = XmlNamespaces.m + "properties";

        public static readonly XName d_id = XmlNamespaces.d + "Id";
        public static readonly XName d_version = XmlNamespaces.d + "Version";

        public static readonly XName baze = XNamespace.Xmlns + "base";
        public static readonly XName m = XNamespace.Xmlns + "m";
        public static readonly XName d = XNamespace.Xmlns + "d";
        public static readonly XName georss = XNamespace.Xmlns + "georss";
        public static readonly XName gml = XNamespace.Xmlns + "gml";
    }
}
