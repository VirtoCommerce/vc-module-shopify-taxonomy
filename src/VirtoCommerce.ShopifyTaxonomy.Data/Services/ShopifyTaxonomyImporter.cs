using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.ShopifyTaxonomy.Core.Common;
using VirtoCommerce.ShopifyTaxonomy.Core.Models;
using VirtoCommerce.ShopifyTaxonomy.Core.Services;

namespace VirtoCommerce.ShopifyTaxonomy.Data.Services
{
    public class ShopifyTaxonomyImporter : IShopifyTaxonomyImporter
    {
        private readonly ICategoryService _categoryService;
        private readonly IPropertyService _propertyService;
        private readonly ICatalogService _catalogService;
        private readonly IPropertyDictionaryItemService _propertyDictionaryItemService;
        private readonly IPushNotificationManager _notifier;
        private readonly IHttpClientFactory _httpClientFactory;

        private const int PageSize = 500;

        public ShopifyTaxonomyImporter(
            ICategoryService categoryService,
            IPropertyService propertyService,
            ICatalogService catalogService,
            IPropertyDictionaryItemService propertyDictionaryItemService,
            IPushNotificationManager notifier,
            IHttpClientFactory httpClientFactory)
        {
            _categoryService = categoryService;
            _propertyService = propertyService;
            _catalogService = catalogService;
            _propertyDictionaryItemService = propertyDictionaryItemService;
            _notifier = notifier;
            _httpClientFactory = httpClientFactory;
        }

        public async Task BackgroundImport(ShopifyTaxonomyImportRequest importInfo, ShopifyTaxonomyImportNotification notifyEvent)
        {
            Action<ExportImportProgressInfo> progressCallback = x =>
            {
                notifyEvent.Description = x.Description;
                notifyEvent.TotalCount = x.TotalCount;
                notifyEvent.ProcessedCount = x.ProcessedCount;
                notifyEvent.Errors = x.Errors ?? new List<string>();

                _notifier.SendAsync(notifyEvent);
            };

            try
            {
                await ImportAsync(importInfo, progressCallback);
            }
            catch (Exception ex)
            {
                notifyEvent.Description = "Export error";
                notifyEvent.Errors.Add(ex.ToString());
            }
            finally
            {
                notifyEvent.Finished = DateTime.UtcNow;
                notifyEvent.Description = "Import finished" + (notifyEvent.Errors.Any() ? " with errors" : " successfully");
                await _notifier.SendAsync(notifyEvent);
            }
        }

        public async Task ImportAsync(ShopifyTaxonomyImportRequest importInfo, Action<ExportImportProgressInfo> progressCallback)
        {
            var progressInfo = new ExportImportProgressInfo
            {
                Description = "Starting Import"
            };
            progressCallback(progressInfo);

            // languages?
            var catalogId = importInfo.CatalogId;

            using var stream = await DownloadMainTaxonomyFileAsync();
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);

            var serializer = new JsonSerializer();
            var taxonomy = serializer.Deserialize<ShopifyTaxonomy.Core.Models.ShopifyTaxonomy>(jsonReader);

            if (taxonomy?.Verticals == null || !taxonomy.Verticals.Any())
            {
                throw new Exception("Invalid taxonomy format");
            }

            // Process all categories from all verticals
            var shopifyCategories = taxonomy.Verticals.SelectMany(v => v.Categories).ToList();
            var outerIdsToCategoryIdsMap = new Dictionary<string, string>();
            var groups = shopifyCategories.GroupBy(x => x.Level);
            var categories = new List<Category>();

            progressInfo.TotalCount = shopifyCategories.Count;

            // First pass - create categories
            foreach (var group in groups)
            {
                var categoryLevel = group.ToList();
                // Process categories in batches
                for (var i = 0; i < categoryLevel.Count; i += PageSize)
                {
                    var batch = categoryLevel.Skip(i).Take(PageSize);
                    var processedCategories = await ProcessCategoryBatch(batch, catalogId, outerIdsToCategoryIdsMap);
                    categories.AddRange(processedCategories);

                    progressInfo.ProcessedCount += processedCategories.Count;
                    progressCallback(progressInfo);
                }
            }

            // Second pass - process attribute mapping and inheritance
            var categoryMap = categories.ToDictionary(c => c.Id);
            var attributeMap = new Dictionary<string, AttributeCategoryWrapper>(); // shopifyAttributeId -> wrapper 
            foreach (var shopifyCategory in shopifyCategories)
            {
                foreach (var shopifyAttribute in shopifyCategory.Attributes ?? [])
                {
                    var categoryId = outerIdsToCategoryIdsMap[shopifyCategory.Id];
                    var category = categoryMap[categoryId];

                    if (attributeMap.TryGetValue(shopifyAttribute.Id, out var wrapper))
                    {
                        if (wrapper.Category != null)
                        {
                            var first = wrapper.Category;
                            var second = category;
                            var lca = CategoryHelper.FindClosestCommonAncestor(categoryMap, first.Id, second.Id);

                            if (lca == null)
                            {
                                // No common ancestor found, use the catalog as container
                                wrapper.Category = null; // Set to null to indicate catalog level
                            }
                            else
                            {
                                wrapper.Category = categoryMap[lca]; // Set to the common ancestor category
                            }
                        }
                    }
                    else
                    {
                        wrapper = new AttributeCategoryWrapper
                        {
                            Category = category, // Initialize with the current category
                        };

                        attributeMap.Add(shopifyAttribute.Id, wrapper);
                    }
                }
            }

            // Third pass - create properties with values
            var properties = new List<Property>();
            var propertyItemsMaps = new Dictionary<string, List<PropertyDictionaryItem>>(); // shopifyAttributeId -> PropertyDictionaryItem
            foreach (var shopifyAttribute in taxonomy.Attributes)
            {
                if (attributeMap.TryGetValue(shopifyAttribute.Id, out var attributeCategoryWrapper))
                {
                    var property = new Property
                    {
                        Name = shopifyAttribute.Handle.Replace('-', '_'),
                        OuterId = shopifyAttribute.Id,
                        Dictionary = true,
                        Multivalue = false,
                        ValueType = PropertyValueType.ShortText,
                        Type = PropertyType.Product,
                    };

                    if (attributeCategoryWrapper.Category != null)
                    {
                        property.CategoryId = attributeCategoryWrapper.Category.Id;
                    }
                    else
                    {
                        property.CatalogId = catalogId; // Use catalog as container if no specific category
                    }

                    // localization
                    property.DisplayNames = new List<PropertyDisplayName>
                    {
                        new PropertyDisplayName
                        {
                            LanguageCode = "en-US",
                            Name = shopifyAttribute.Name,
                        }
                    };

                    // values
                    var dictionaryItems = new List<PropertyDictionaryItem>();
                    foreach (var shopifyValue in shopifyAttribute.Values ?? [])
                    {
                        var value = new PropertyDictionaryItem
                        {
                            Alias = shopifyValue.Handle,
                            LocalizedValues = new[]
                                {
                                    new PropertyDictionaryItemLocalizedValue { LanguageCode = "en-US", Value = shopifyValue.Name,  },
                                },
                        };

                        dictionaryItems.Add(value);
                    }

                    properties.Add(property);
                    propertyItemsMaps.Add(shopifyAttribute.Id, dictionaryItems);
                }
            }

            for (var i = 0; i < properties.Count; i += PageSize)
            {
                var batch = properties.Skip(i).Take(PageSize).ToList();

                await _propertyService.SaveChangesAsync(batch);

                // Save dictionary items
                foreach (var property in batch)
                {
                    if (propertyItemsMaps.TryGetValue(property.OuterId, out var dictionaryItems))
                    {
                        foreach (var item in dictionaryItems)
                        {
                            item.PropertyId = property.Id;
                        }

                        await _propertyDictionaryItemService.SaveChangesAsync(dictionaryItems);
                    }
                }
            }
        }

        private async Task<List<Category>> ProcessCategoryBatch(IEnumerable<ShopifyCategory> shopifyCategories, string catalogId, Dictionary<string, string> outerIdsToCategoryIdsMap)
        {
            var categories = new List<Category>();
            foreach (var shopifyCategory in shopifyCategories)
            {
                // Create or update category
                var category = new Category
                {
                    Name = shopifyCategory.Name,
                    OuterId = shopifyCategory.Id,
                    Code = shopifyCategory.Id.Split('/').Last(), // Use the last part of the Shopify ID as code
                    IsActive = true,
                    CatalogId = catalogId,
                };

                // Set parent if exists
                if (!string.IsNullOrEmpty(shopifyCategory.ParentId))
                {
                    if (outerIdsToCategoryIdsMap.TryGetValue(shopifyCategory.ParentId, out var parentId))
                    {
                        category.ParentId = parentId;
                    }
                }

                categories.Add(category);
            }

            // Save categories in bulk
            await _categoryService.SaveChangesAsync(categories);

            foreach (var category in categories)
            {
                outerIdsToCategoryIdsMap.TryAdd(category.OuterId, category.Id); // Map outerId to internal Id
            }

            return categories;
        }

        public async Task<Stream> DownloadMainTaxonomyFileAsync()
        {
            var client = _httpClientFactory.CreateClient();

            var fileUrl = "https://raw.githubusercontent.com/Shopify/product-taxonomy/refs/heads/main/dist/en/taxonomy.json";

            var response = await client.GetAsync(fileUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to download the taxonomy file.");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return stream;
        }

        private class AttributeCategoryWrapper
        {
            public Category Category { get; set; } // Null category means use Catalog as container - skip finding common ancestor then
        }
    }
}
