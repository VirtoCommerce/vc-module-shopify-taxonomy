using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.ShopifyTaxonomy.Core.Models;
using VirtoCommerce.ShopifyTaxonomy.Core.Services;
using CatalogModuleConstants = VirtoCommerce.CatalogModule.Core.ModuleConstants;

namespace VirtoCommerce.CatalogModule.Web.Controllers.Api
{
    [Route("api/shopify-taxonomy")]
    [ApiController]
    public class ShopifyTaxonomyController : ControllerBase
    {
        private readonly ICatalogService _catalogService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserNameResolver _userNameResolver;
        private readonly IShopifyTaxonomyImporter _shopifyTaxonomyImporter;
        private readonly IPushNotificationManager _notifier;

        public ShopifyTaxonomyController(
            ICatalogService catalogService,
            IAuthorizationService authorizationService,
            IUserNameResolver userNameResolver,
            IPushNotificationManager notifier,
            IShopifyTaxonomyImporter shopifyTaxonomyImporter)
        {
            _catalogService = catalogService;
            _authorizationService = authorizationService;
            _userNameResolver = userNameResolver;
            _notifier = notifier;
            _shopifyTaxonomyImporter = shopifyTaxonomyImporter;
        }

        [HttpPost]
        [Route("import")]
        //[Authorize(Security.Permissions.Import)]
        //[ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ShopifyTaxonomyImportNotification), StatusCodes.Status200OK)]

        public async Task<ActionResult<ShopifyTaxonomyImportNotification>> DoImport([FromBody] ShopifyTaxonomyImportRequest importInfo)
        {
            var hasPermissions = true;

            if (!importInfo.CatalogId.IsNullOrEmpty())
            {
                var catalog = await _catalogService.GetNoCloneAsync(importInfo.CatalogId);

                if (catalog != null)
                {
                    hasPermissions = await CheckCatalogPermission(catalog, CatalogModuleConstants.Security.Permissions.Update);
                }
            }

            if (!hasPermissions)
            {
                return Unauthorized();
            }

            var criteria = AbstractTypeFactory<CatalogSearchCriteria>.TryCreateInstance();
            criteria.CatalogIds = new[] { importInfo.CatalogId };

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, criteria, new CatalogAuthorizationRequirement(CatalogModuleConstants.Security.Permissions.Update));
            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }

            var notification = new ShopifyTaxonomyImportNotification(_userNameResolver.GetCurrentUserName())
            {
                Title = "Import Shopify Taxonomy",
                Description = "starting import...."
            };
            await _notifier.SendAsync(notification);

            BackgroundJob.Enqueue(() => _shopifyTaxonomyImporter.BackgroundImport(importInfo, notification));

            return Ok(notification);
        }

        private async Task<bool> CheckCatalogPermission(object checkedEntities, string permission)
        {
            var result = true;
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, checkedEntities, new CatalogAuthorizationRequirement(permission));

            if (!authorizationResult.Succeeded)
            {
                result = false;
            }

            return result;
        }
    }
}
