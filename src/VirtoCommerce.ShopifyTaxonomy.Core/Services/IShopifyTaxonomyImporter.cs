using System;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.ShopifyTaxonomy.Core.Models;

namespace VirtoCommerce.ShopifyTaxonomy.Core.Services;

public interface IShopifyTaxonomyImporter
{
    Task BackgroundImport(ShopifyTaxonomyImportRequest importInfo, ShopifyTaxonomyImportNotification notifyEvent);
    Task ImportAsync(ShopifyTaxonomyImportRequest importInfo, Action<ExportImportProgressInfo> progressCallback);
}
