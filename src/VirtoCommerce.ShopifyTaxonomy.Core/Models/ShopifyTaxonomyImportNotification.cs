using VirtoCommerce.Platform.Core.ExportImport.PushNotifications;

namespace VirtoCommerce.ShopifyTaxonomy.Core.Models;

public class ShopifyTaxonomyImportNotification : PlatformExportImportPushNotification
{
    public ShopifyTaxonomyImportNotification(string creator) : base(creator)
    {
        NotifyType = "ShopifyTaxonomyImport";
    }
}
