using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Exceptions;
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

        private List<string> Languages => [
            "bg-BG", // Bulgarian
            "cs",    // Czech
            "da",    // Danish
            "de",    // German
            "el",    // Greek
            "en",    // English
            "es",    // Spanish
            "fi",    // Finnish
            "fr",    // French
            "hr-HR", // Croatian
            "hu",    // Hungarian
            "id-ID", // Indonesian
            "it",    // Italian
            "ja",    // Japanese
            "ko",    // Korean
            "lt-LT", // Lithuanian
            "nb",    // Norwegian
            "nl",    // Dutch
            "pl",    // Polish
            "pt-BR", // Portuguese (Brazil)
            "pt-PT", // Portuguese (Portugal)
            "ro-RO", // Romanian
            "ru",    // Russian
            "sk-SK", // Slovak
            "sl-SI", // Slovenian
            "sv",    // Swedish
            "th",    // Thai
            "tr",    // Turkish
            "vi",    // Vietnamese
            "zh-CN", // Chinese (Simplified)
            "zh-TW", // Chinese (Traditional)
        ];

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

        public async Task ImportAsync(ShopifyTaxonomyImportRequest importRequest, Action<ExportImportProgressInfo> progressCallback)
        {
            var progressInfo = new ExportImportProgressInfo
            {
                Description = "Starting Import"
            };
            progressCallback(progressInfo);

            var catalogId = importRequest.CatalogId;

            // find languages
            var catalog = await _catalogService.GetNoCloneAsync(catalogId);
            var defaultLangugae = catalog.DefaultLanguage.LanguageCode;
            var mainTaxonomyFileUrl = GetTaxonomyFileUrl(defaultLangugae);

            progressInfo.Description = "Downloading main taxonomy file";
            progressCallback(progressInfo);

            using var stream = await DownloadMainTaxonomyFileAsync(mainTaxonomyFileUrl);
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);

            var serializer = new JsonSerializer();
            var taxonomy = serializer.Deserialize<Core.Models.ShopifyTaxonomy>(jsonReader);

            if (taxonomy?.Verticals == null)
            {
                throw new PlatformException("Invalid taxonomy format");
            }

            // Process all categories from all verticals
            var shopifyCategories = taxonomy.Verticals.SelectMany(v => v.Categories).ToList();

            progressInfo.TotalCount = shopifyCategories.Count;

            var localizations = await GetLocalizationResources(defaultLangugae, importRequest, progressCallback, progressInfo, catalog, serializer);

            foreach (var localization in localizations)
            {
                foreach (var category in localization.Categories)
                {
                    // Find the corresponding category in the main taxonomy
                    var mainCategory = shopifyCategories.FirstOrDefault(c => c.Id == category.Id);

                    if (mainCategory != null)
                    {
                        mainCategory.LocalizedName ??= new LocalizedString();
                        mainCategory.LocalizedName.Values.TryAdd(localization.CultureName, category.Name); // Add localized name
                    }
                }
            }

            progressInfo.Description = "Processing categories";
            progressCallback(progressInfo);

            // First pass - create categories
            var categoriesResult = await ProcessCategories(progressCallback, progressInfo, catalogId, defaultLangugae, shopifyCategories);

            if (!importRequest.ImportProperties)
            {
                return;
            }

            progressInfo.Description = "Processing attributes";
            progressCallback(progressInfo);

            // Second pass - process attribute mapping and inheritance
            var attributeMap = ProcessAttributeInheritance(shopifyCategories, categoriesResult);

            // Third pass - create properties with values
            var propertiesResult = ProcessProperties(importRequest, catalogId, defaultLangugae, taxonomy, localizations, attributeMap);

            progressInfo.TotalCount = propertiesResult.Properties.Count;
            progressInfo.ProcessedCount = 0;
            progressCallback(progressInfo);

            await SaveProperties(progressCallback, progressInfo, propertiesResult);
        }

        private async Task SaveProperties(Action<ExportImportProgressInfo> progressCallback, ExportImportProgressInfo progressInfo, ProcessPropertiesResult propertiesResult)
        {
            for (var i = 0; i < propertiesResult.Properties.Count; i += PageSize)
            {
                var batch = propertiesResult.Properties.Skip(i).Take(PageSize).ToList();

                await _propertyService.SaveChangesAsync(batch);

                // Save dictionary items
                foreach (var property in batch)
                {
                    if (propertiesResult.PropertyItemsMaps.TryGetValue(property.OuterId, out var dictionaryItems))
                    {
                        foreach (var item in dictionaryItems)
                        {
                            item.PropertyId = property.Id;
                        }

                        await _propertyDictionaryItemService.SaveChangesAsync(dictionaryItems);
                    }
                }

                progressInfo.ProcessedCount += batch.Count;
                progressCallback(progressInfo);
            }
        }

        private static ProcessPropertiesResult ProcessProperties(ShopifyTaxonomyImportRequest importRequest,
            string catalogId,
            string defaultLangugae,
            Core.Models.ShopifyTaxonomy taxonomy,
            List<LocalizedTaxonomyResource> localizations,
            Dictionary<string, AttributeCategoryWrapper> attributeMap)
        {
            var result = new ProcessPropertiesResult();

            foreach (var shopifyAttribute in taxonomy.Attributes)
            {
                if (!attributeMap.TryGetValue(shopifyAttribute.Id, out var attributeCategoryWrapper))
                {
                    continue;
                }

                var property = new Property
                {
                    Name = shopifyAttribute.Handle.Replace('-', '_'),
                    OuterId = shopifyAttribute.Id,
                    Dictionary = true,
                    Multilanguage = true,
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
                            LanguageCode = defaultLangugae,
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
                        LocalizedValues = new List<PropertyDictionaryItemLocalizedValue>
                                {
                                    new PropertyDictionaryItemLocalizedValue
                                    {
                                        LanguageCode = defaultLangugae,
                                        Value = shopifyValue.Name,
                                    },
                                },
                    };

                    dictionaryItems.Add(value);
                }

                result.Properties.Add(property);
                result.PropertyItemsMaps.Add(shopifyAttribute.Id, dictionaryItems);

                // localizations
                if (!importRequest.ImportLocalizations)
                {
                    continue;
                }

                foreach (var localization in localizations)
                {
                    var localizedAttribue = localization.Attributes.FirstOrDefault(x => x.Id == shopifyAttribute.Id);
                    if (localizedAttribue == null)
                    {
                        continue;
                    }

                    var localizedName = new PropertyDisplayName
                    {
                        LanguageCode = localization.CultureName,
                        Name = localizedAttribue.Name,
                    };
                    property.DisplayNames.Add(localizedName);

                    // dictionary values
                    foreach (var dictionaryItem in dictionaryItems)
                    {
                        var shopifyLocalizedValue = localizedAttribue.Values?.FirstOrDefault(x => x.Handle == dictionaryItem.Alias);
                        if (shopifyLocalizedValue == null)
                        {
                            continue;
                        }

                        var localizedItemValue = new PropertyDictionaryItemLocalizedValue
                        {
                            LanguageCode = localization.CultureName,
                            Value = shopifyLocalizedValue.Name,
                        };
                        dictionaryItem.LocalizedValues.Add(localizedItemValue);
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, AttributeCategoryWrapper> ProcessAttributeInheritance(List<ShopifyCategory> shopifyCategories, ProcessCatetgoriesResult catetgoriesResult)
        {
            var attributeMap = new Dictionary<string, AttributeCategoryWrapper>(); // shopifyAttributeId -> wrapper

            var categoryMap = catetgoriesResult.Categories.ToDictionary(c => c.Id);

            foreach (var shopifyCategory in shopifyCategories)
            {
                foreach (var shopifyAttributeId in shopifyCategory.Attributes.Select(x => x.Id))
                {
                    var categoryId = catetgoriesResult.OuterIdsToCategoryIdsMap[shopifyCategory.Id];
                    var category = categoryMap[categoryId];

                    if (attributeMap.TryGetValue(shopifyAttributeId, out var wrapper))
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

                        attributeMap.Add(shopifyAttributeId, wrapper);
                    }
                }
            }

            return attributeMap;
        }

        private async Task<ProcessCatetgoriesResult> ProcessCategories(Action<ExportImportProgressInfo> progressCallback,
            ExportImportProgressInfo progressInfo,
            string catalogId,
            string defaultLangugae,
            List<ShopifyCategory> shopifyCategories)
        {
            var result = new ProcessCatetgoriesResult();

            var groups = shopifyCategories.GroupBy(x => x.Level);
            foreach (var group in groups)
            {
                var categoryLevel = group.ToList();
                // Process categories in batches
                for (var i = 0; i < categoryLevel.Count; i += PageSize)
                {
                    var batch = categoryLevel.Skip(i).Take(PageSize);
                    var processedCategories = await ProcessCategoryBatch(batch, catalogId, result.OuterIdsToCategoryIdsMap, defaultLangugae);
                    result.Categories.AddRange(processedCategories);

                    progressInfo.ProcessedCount += processedCategories.Count;
                    progressCallback(progressInfo);
                }
            }

            return result;
        }

        private async Task<List<LocalizedTaxonomyResource>> GetLocalizationResources(string defaultLangugae,
            ShopifyTaxonomyImportRequest importRequest,
            Action<ExportImportProgressInfo> progressCallback,
            ExportImportProgressInfo progressInfo,
            Catalog catalog,
            JsonSerializer serializer)
        {
            var localizations = new List<LocalizedTaxonomyResource>();

            if (importRequest.ImportLocalizations)
            {
                // download all taxonomy files and create localization maps
                var langugages = catalog.Languages.Select(x => x.LanguageCode).Where(x => !x.EqualsIgnoreCase(defaultLangugae));

                foreach (var langugage in langugages)
                {
                    var taxonomyFileUrl = GetTaxonomyFileUrl(langugage);
                    if (taxonomyFileUrl != null)
                    {
                        localizations.Add(new LocalizedTaxonomyResource
                        {
                            CultureName = langugage,
                            TaxonomyFileUrl = taxonomyFileUrl,
                        });
                    }
                }

                foreach (var localization in localizations)
                {
                    var cultureName = localization.CultureName;
                    var fileUrl = localization.TaxonomyFileUrl;

                    using var localizationFileStream = await DownloadMainTaxonomyFileAsync(fileUrl);

                    progressInfo.Description = $"Downloading localization file for {cultureName}";
                    progressCallback(progressInfo);

                    using var localizationFileReader = new StreamReader(localizationFileStream);
                    using var localizedJsonReader = new JsonTextReader(localizationFileReader);

                    var localizedTaxonomy = serializer.Deserialize<Core.Models.ShopifyTaxonomy>(localizedJsonReader);

                    // category localizations
                    localization.Categories = localizedTaxonomy.Verticals.SelectMany(v => v.Categories).ToList();

                    //// Merge localized taxonomy into main taxonomy
                    //foreach (var vertical in localizedTaxonomy.Verticals)
                    //{
                    //    foreach (var category in vertical.Categories)
                    //    {
                    //        // Find the corresponding category in the main taxonomy
                    //        var mainCategory = shopifyCategories.FirstOrDefault(c => c.Id == category.Id);

                    //        if (mainCategory != null)
                    //        {
                    //            mainCategory.LocalizedName ??= new LocalizedString();
                    //            mainCategory.LocalizedName.Values.TryAdd(localization.CultureName, category.Name); // Add localized name
                    //        }
                    //    }
                    //}

                    // property and property values localizations
                    if (importRequest.ImportProperties)
                    {
                        localization.Attributes = localizedTaxonomy.Attributes;
                    }
                }
            }

            return localizations;
        }

        private async Task<List<Category>> ProcessCategoryBatch(IEnumerable<ShopifyCategory> shopifyCategories,
            string catalogId,
            Dictionary<string, string> outerIdsToCategoryIdsMap,
            string defaultLanguage)
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

                var localizations = shopifyCategory.LocalizedName?.GetCopy() as LocalizedString;
                localizations ??= new LocalizedString();
                localizations.Values.TryAdd(defaultLanguage, shopifyCategory.Name);
                category.LocalizedName = localizations;

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

        private async Task<Stream> DownloadMainTaxonomyFileAsync(string fileUrl)
        {
            var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync(fileUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new PlatformException("Failed to download the taxonomy file.");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return stream;
        }

        private string GetTaxonomyFileUrl(string cultureName)
        {
            var result = default(string);

            var language = cultureName.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (language.Length > 0)
            {
                var availableLanguage = Languages.FirstOrDefault(x => x.EqualsIgnoreCase(language[0]));
                availableLanguage ??= Languages.FirstOrDefault(x => x.EqualsIgnoreCase(cultureName));

                if (availableLanguage != null)
                {
                    result = $"https://raw.githubusercontent.com/Shopify/product-taxonomy/refs/heads/main/dist/{availableLanguage}/taxonomy.json";
                }
            }

            return result;
        }


        private sealed class AttributeCategoryWrapper
        {
            public Category Category { get; set; } // Null category means use Catalog as container - skip finding common ancestor then
        }

        private sealed class LocalizedTaxonomyResource
        {
            public string CultureName { get; set; }

            public string TaxonomyFileUrl { get; set; }

            // Categories
            public List<ShopifyCategory> Categories { get; set; } = [];

            // Properties and values
            public List<ShopifyAttribute> Attributes { get; set; } = [];
        }

        private sealed class ProcessCatetgoriesResult
        {
            public List<Category> Categories { get; set; } = [];

            public Dictionary<string, string> OuterIdsToCategoryIdsMap { get; set; } = [];
        }

        private sealed class ProcessPropertiesResult
        {
            public List<Property> Properties { get; set; } = [];

            public Dictionary<string, List<PropertyDictionaryItem>> PropertyItemsMaps { get; set; } = []; // shopifyAttributeId -> PropertyDictionaryItem
        }
    }
}
