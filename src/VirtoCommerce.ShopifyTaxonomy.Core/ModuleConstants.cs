using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ShopifyTaxonomy.Core;

public static class ModuleConstants
{
    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor ShopifyTaxonomyMainFileUrl { get; } = new()
            {
                Name = "ShopifyTaxonomy.ShopifyTaxonomyFileUrl",
                GroupName = "ShopifyTaxonomyImport|General",
                ValueType = SettingValueType.ShortText,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return ShopifyTaxonomyMainFileUrl;
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
