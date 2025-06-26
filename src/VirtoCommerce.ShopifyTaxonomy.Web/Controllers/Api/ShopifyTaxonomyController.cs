using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VirtoCommerce.ShopifyTaxonomy.Web.Controllers.Api;

[Authorize]
[Route("api/shopify-taxonomy")]
public class ShopifyTaxonomyController : Controller
{
}
