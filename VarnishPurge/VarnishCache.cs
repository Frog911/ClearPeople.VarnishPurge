using System;
using System.Net;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.Resources.Media;

namespace ClearPeople.VarnishPurge
{
    /// <summary>
    /// Manages the Varnish cache requests
    /// </summary>
    public class VarnishCache
    {
        private static readonly UrlOptions _urlOptions;

        /// <summary>
        /// Initializes the <see cref="VarnishCache"/> class.
        /// </summary>
        static VarnishCache()
        {
            _urlOptions = new UrlOptions { Site = Factory.GetSite(VarnishSettings.Site), SiteResolving = true, LanguageEmbedding = LanguageEmbedding.Never };
        }

        #region " Public methods "

        /// <summary>
        /// Clears the cache for the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        public static void Clear(Item item)
        {
            Clear(GetFullItemUrl(item));
        }

        /// <summary>
        /// Clears the cache for the given url.
        /// URL must be relative to the url at Varnish Settings
        /// </summary>
        /// <param name="url">The url to clear.</param>
        public static void Clear(string url)
        {
            StartWebRequest(url, VarnishSettings.HttpMethod);
        }

        /// <summary>
        /// Clears the cached objects for all the site.
        /// </summary>
        public static void ClearSite()
        {
            StartWebRequest(VarnishSettings.Url, VarnishSettings.HttpClearMethod);
        }

        /// <summary>
        /// Gets the full item URL, including the Host URI stored at "Varnish.Url" setting.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public static string GetFullItemUrl(Item item)
        {
            return VarnishSettings.Url + GetRelativeItemUrl(item);
        }

        /// <summary>
        /// Gets the relative item URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public static string GetRelativeItemUrl(Item item)
        {
            return item.Paths.IsMediaItem
                ? MediaManager.GetMediaUrl(item)
                : LinkManager.GetItemUrl(item, _urlOptions);
        }

        #endregion

        #region " Private methods "

        /// <summary>
        /// Starts the web request.
        /// </summary>
        /// <param name="url">The item.</param>
        /// <param name="method">The default HTTP method.</param>
        private static void StartWebRequest(string url, string method)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                if (!String.IsNullOrEmpty(VarnishSettings.Host))
                    request.Host = VarnishSettings.Host;
                request.BeginGetResponse(FinishWebRequest, request);
            }
            catch (Exception ex)
            {
                Log.Error("[" + VarnishSettings.LogHeader + "] ERROR clearing " + url + " : " + ex.Message + Environment.NewLine + ex.StackTrace, typeof(VarnishCache));
            }
        }

        /// <summary>
        /// Finishes the web request.
        /// </summary>
        /// <param name="result">The result.</param>
        private static void FinishWebRequest(IAsyncResult result)
        {
            HttpWebResponse response = ((HttpWebRequest)result.AsyncState).EndGetResponse(result) as HttpWebResponse;
            if (response == null)
            {
                Log.Error("[" + VarnishSettings.LogHeader + "] ERROR: The server didn't return any Response", typeof(VarnishCache));
                return;
            }
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Log.Info("[" + VarnishSettings.LogHeader + "] Url cleared: " + response.ResponseUri, typeof(VarnishCache));
                    break;
                default:
                    Log.Error("[" + VarnishSettings.LogHeader + "] ERROR: Server returned " + response.StatusCode + " code for " + response.ResponseUri + ". " + response.StatusDescription, typeof(VarnishCache));
                    break;
            }
        }

        #endregion

    }
}
