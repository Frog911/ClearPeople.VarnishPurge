using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sitecore.Data.Comparers;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.Publishing.Pipelines.Publish;

namespace ClearPeople.VarnishPurge
{
    /// <summary>
    /// Varnish custom processor
    /// </summary>
    public class VarnishPurgeProcessor : PublishProcessor
    {
        private readonly HashSet<string> _purgedUrls = new HashSet<string>();

        /// <summary>
        /// Processes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Process(PublishContext context)
        {
            if (!VarnishSettings.Enabled) return;

            var global = new Stopwatch();
            global.Start();

            Assert.ArgumentNotNull(context, "context");
            if (context == null || context.PublishOptions == null || context.PublishOptions.TargetDatabase == null)
            {
                Log.Error("[" + VarnishSettings.LogHeader + "] ERROR: Context and/or publish settings are null", this);
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

            ClearLocalCache();
            Log.Info("[" + VarnishSettings.LogHeader + "] Cache for '" + rootItem.Name + "' cleared in " + global.Elapsed, this);
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
        /// Purges the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="publishRelatedItems">if set to <c>true</c> also tries to purge the related items.</param>
        private void PurgeItem(Item item, bool publishRelatedItems)
        {
            if ((item.Paths.IsMediaItem && !item.HasChildren) || item.Paths.IsContentItem)
            {
                if(AddToLocalCache(item))
                    VarnishCache.Clear(item);
            }

            if (!publishRelatedItems) return;

            var referenced = GetReferences(item);
            foreach (var relatedItem in referenced.Where(relatedItem => (relatedItem.Paths.IsMediaItem && !relatedItem.HasChildren) || relatedItem.Paths.IsContentItem).Where(AddToLocalCache))
            {
                VarnishCache.Clear(relatedItem);
            }
        }

        /// <summary>
        /// Adds the url to the local cache, to avoid duplicating requests.
        /// </summary>
        /// <param name="item">The item.</param>
        private bool AddToLocalCache(Item item)
        {
            var url = VarnishCache.GetRelativeItemUrl(item);
            var finalUrl = VarnishSettings.Url + url;
            var cachedUrl = String.IsNullOrEmpty(VarnishSettings.Host) ? finalUrl : "http://" + VarnishSettings.Host + url;
            if (_purgedUrls.Contains(cachedUrl)) return false;

            _purgedUrls.Add(cachedUrl);
            return true;
        }

        /// <summary>
        /// Clears the local cache.
        /// </summary>
        private void ClearLocalCache()
        {
            _purgedUrls.Clear();
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

    }
}