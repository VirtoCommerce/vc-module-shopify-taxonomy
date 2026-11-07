using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Jobs;
using VirtoCommerce.ShopifyTaxonomy.Core.Models;
using VirtoCommerce.ShopifyTaxonomy.Core.Services;

namespace VirtoCommerce.ShopifyTaxonomy.Web.BackgroundJobs;

/// <summary>
/// Payload of <see cref="ShopifyTaxonomyImportJob"/>: the import request and the push notification that reports its progress.
/// </summary>
public class ShopifyTaxonomyImportJobPayload
{
    public ShopifyTaxonomyImportRequest Request { get; set; }
    public ShopifyTaxonomyImportNotification Notification { get; set; }
}

/// <summary>
/// Engine-agnostic Shopify taxonomy import job. Replaces the former Hangfire job that ran
/// <see cref="IShopifyTaxonomyImporter.BackgroundImport"/> directly; the import itself is unchanged.
/// </summary>
public class ShopifyTaxonomyImportJob(IShopifyTaxonomyImporter shopifyTaxonomyImporter) : IBackgroundJobHandler<ShopifyTaxonomyImportJobPayload>
{
    public virtual Task Execute(ShopifyTaxonomyImportJobPayload payload, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return shopifyTaxonomyImporter.BackgroundImport(payload.Request, payload.Notification);
    }
}
