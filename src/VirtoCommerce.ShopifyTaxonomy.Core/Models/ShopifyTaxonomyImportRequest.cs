namespace VirtoCommerce.ShopifyTaxonomy.Core.Models;

public class ShopifyTaxonomyImportRequest
{
    public string CatalogId { get; set; }

    public bool ImportProperties { get; set; }

    public bool ImportLocalizations { get; set; }
}
