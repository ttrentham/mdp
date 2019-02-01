using System;

namespace Athenahealth
{
    /// <summary>
    /// It's expected that you override MDPKey and MDPSecret with your own values in LocalConfig.cs
    /// That file is part of the gitignore for the project to allow you to specify your key and secret
    /// without the risk of exposing to public source control
    /// </summary>
    public static partial class Config
    {
        public static readonly string MDPKey = "";
        public static readonly string MDPSecret = "";

        public static readonly UriBuilder MDPUrl = new UriBuilder("https", "api.athenahealth.com", -1);

    }
}
