using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ShopifyTaxonomy.Core;

public static class ModuleConstants
{
    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor ShopifyTaxonomyFileUrl { get; } = new()
            {
                Name = "ShopifyTaxonomy.ShopifyTaxonomyFileUrl",
                GroupName = "ShopifyTaxonomyImport|General",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "https://raw.githubusercontent.com/Shopify/product-taxonomy/refs/heads/main/dist/{languageCode}/taxonomy.json",
            };

            public static SettingDescriptor ShopifyTaxonomyLanguagesCodes { get; } = new()
            {
                Name = "ShopifyTaxonomy.ShopifyTaxonomyLanguagesCodes",
                GroupName = "ShopifyTaxonomyImport|General",
                ValueType = SettingValueType.ShortText,
                IsDictionary = true,
                DefaultValue = "en",
                AllowedValues = [
                    "en",    // English
                    "bg-BG", // Bulgarian
                    "cs",    // Czech
                    "da",    // Danish
                    "de",    // German
                    "el",    // Greek
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
                ],
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return ShopifyTaxonomyFileUrl;
                    yield return ShopifyTaxonomyLanguagesCodes;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
