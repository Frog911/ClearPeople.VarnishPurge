using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Sitecore.Configuration;
using Sitecore.Data.Comparers;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Resources.Media;

namespace ClearPeople.VarnishPurge
{
    public class VarnishPurgeProcessor : PublishProcessor
    {
        private readonly UrlOptions _urlOptions;
        private readonly HashSet<string> _purgedUrls = new HashSet<string>();

        public VarnishPurgeProcessor()
        {
            _urlOptions = new UrlOptions { Site = Factory.GetSite(VarnishSettings.Site), SiteResolving = true, LanguageEmbedding = LanguageEmbedding.Never };
        }

        public override void Process(PublishContext context)
        {
            if (!VarnishSettings.Enabled) return;

            var global = new Stopwatch();
            global.Start();

            Assert.ArgumentNotNull(context, "context");
            if (context == null || context.PublishOptions == null || context.PublishOptions.TargetDatabase == null)
            {
                Log.Error("Context and/or publish settings are null", this);
                return;
            }

            var rootItem = context.PublishOptions.RootItem;
            if (!context.PublishOptions.Deep)
            {
                PurgeItem(rootItem, context.PublishOptions.PublishRelatedItems);
            }
            else
            {
                PurgeItems(rootItem, context.PublishOptions.PublishRelatedItems);
            }
            
            _purgedUrls.Clear();
            Log.Info("[Varnish] Cache for '" + rootItem.Name + "' cleared in " + global.Elapsed, this);
        }

        /// <summary>
        /// Purges the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="publishRelatedItems">if set to <c>true</c> also tries to purge the related items.</param>
        private void PurgeItem(Item item, bool publishRelatedItems)
        {
            if ((item.Paths.IsMediaItem && !item.HasChildren) || item.Paths.IsContentItem)
                StartWebRequest(item);

            if (!publishRelatedItems) return;

            var referenced = GetReferences(item);
            foreach (var relatedItem in referenced.Where(relatedItem => (relatedItem.Paths.IsMediaItem && !relatedItem.HasChildren) || relatedItem.Paths.IsContentItem))
            {
                StartWebRequest(relatedItem);
            }
        }

        /// <summary>
        /// Purges the children items.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="publishRelatedItems">if set to <c>true</c> also tries to purge the related items.</param>
        private void PurgeItems(Item item, bool publishRelatedItems)
        {
            PurgeItem(item, publishRelatedItems);

            foreach (Item child in item.GetChildren())
            {
                PurgeItems(child, publishRelatedItems);
            }
        }

        /// <summary>
        /// Gets the referenced items.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        private IEnumerable<Item> GetReferences(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            List<Item> items = new List<Item>();
            ItemLink[] validLinks = item.Links.GetValidLinks();
            validLinks = (
                from link in validLinks
                where item.Database.Name.Equals(link.TargetDatabaseName, StringComparison.OrdinalIgnoreCase)
                select link).ToArray<ItemLink>();

            List<Item> list = (
                from link in (IEnumerable<ItemLink>)validLinks
                select link.GetTargetItem() into relatedItem
                where relatedItem != null
                select relatedItem).ToList<Item>();
            foreach (Item item1 in list)
            {
                items.AddRange(PublishQueue.GetParents(item1));
                items.Add(item1);
            }
            return items.Distinct(new ItemIdComparer());
        }

        /// <summary>
        /// Starts the web request.
        /// </summary>
        /// <param name="item">The item.</param>
        private void StartWebRequest(Item item)
        {
            string url = item.Paths.IsMediaItem
                ? MediaManager.GetMediaUrl(item)
                : LinkManager.GetItemUrl(item, _urlOptions);
            var finalUrl = VarnishSettings.Url + url;
            var cachedUrl = String.IsNullOrEmpty(VarnishSettings.Host) ? finalUrl : "http://" + VarnishSettings.Host + url;
            if (_purgedUrls.Contains(cachedUrl)) return;

            _purgedUrls.Add(cachedUrl);
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(finalUrl);
                request.Method = VarnishSettings.HttpMethod;
                if (!String.IsNullOrEmpty(VarnishSettings.Host))
                    request.Host = VarnishSettings.Host;
                request.BeginGetResponse(FinishWebRequest, request);
            }
            catch (Exception ex)
            {
                Log.Error("[Varnish ERROR] " + ex.Message + Environment.NewLine + ex.StackTrace, this);
            }
        }

        /// <summary>
        /// Finishes the web request.
        /// </summary>
        /// <param name="result">The result.</param>
        private void FinishWebRequest(IAsyncResult result)
        {
            HttpWebResponse response = ((HttpWebRequest)result.AsyncState).EndGetResponse(result) as HttpWebResponse;
            if (response == null)
            {
                Log.Error("[Varnish ERROR] The server didn't return any Response.", this);
                return;
            }
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Log.Info("[Varnish] Url cleared: " + response.ResponseUri, this);
                    break;
                default:
                    Log.Error("[Varnish ERROR] Server returned " + response.StatusCode + " code. " + response.StatusDescription, this);
                    break;
            }
        }
    }
}