using System;
using Sitecore.Configuration;

namespace ClearPeople.VarnishPurge
{
    /// <summary>
    /// Mapping Module Settings from App_Config/Include/VarnishPipeline.config
    /// </summary>
    public static class VarnishSettings
    {
        private static string _site;
        private static string _url;
        private static string _host;
        private static string _method;
        private static string _enabledValue;
        private static bool _enabled;

        /// <summary>
        /// Gets the Sitecore site. Used to get proper URLs from items
        /// </summary>
        /// <value>
        /// The site.
        /// </value>
        public static String Site
        {
            get
            {
                if (!String.IsNullOrEmpty(_site))
                    return _site;

                _site = String.IsNullOrEmpty(Settings.GetSetting("Varnish.Site"))
                    ? "website"
                    : Settings.GetSetting("Varnish.Site");
                return _site;
            }
        }

        /// <summary>
        /// Gets the Actual Varnish server URL (Must start with "HTTP://"), proxy free
        /// </summary>
        /// <value>
        /// The URL.
        /// </value>
        public static String Url
        {
            get
            {
                if (!String.IsNullOrEmpty(_url))
                    return _url;

                _url = Settings.GetSetting("Varnish.Url");
                return _url;
            }
        }

        /// <summary>
        /// Gets the Direct host to avoid proxies like CloudFlare. If not empty, WebRequest will go to "Varnish.Url", with a HOST HTTP Header with this value
        /// </summary>
        /// <value>
        /// The host.
        /// </value>
        public static String Host
        {
            get
            {
                if (!String.IsNullOrEmpty(_host))
                    return _host;

                _host = Settings.GetSetting("Varnish.Direct.Host");
                return _host;
            }
        }

        /// <summary>
        /// Gets the HTTP Method used. Common values are "PURGE" or "DELETE"
        /// "PURGE" by default if it's empty
        /// </summary>
        /// <value>
        /// The HTTP method.
        /// </value>
        public static String HttpMethod
        {
            get
            {
                if (!String.IsNullOrEmpty(_method))
                    return _method;

                _method = String.IsNullOrEmpty(Settings.GetSetting("Varnish.Method"))
                            ? "PURGE"
                            : Settings.GetSetting("Varnish.Method");
                return _method;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this module is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if enabled; otherwise, <c>false</c>.
        /// </value>
        public static Boolean Enabled
        {
            get
            {
                if (!String.IsNullOrEmpty(_enabledValue))
                    return _enabled;

                _enabledValue = Settings.GetSetting("Varnish.Enabled");
                Boolean.TryParse(_enabledValue, out _enabled);
                return _enabled;
            }
        }


    }
}
