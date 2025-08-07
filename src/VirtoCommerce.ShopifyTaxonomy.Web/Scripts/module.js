// Call this to register your module to main application
var moduleName = 'VirtoCommerce.ShopifyTaxonomy';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .run(['virtoCommerce.catalogModule.catalogImportService',
        function (catalogImportService) {
            catalogImportService.register({
                name: 'Shopify Taxonomy Import',
                description: 'Import From Shopify Taxonomy',
                icon: 'fa fa-list-alt',
                controller: 'VirtoCommerce.ShopifyTaxonomy.taxonomyImportController',
                template: 'Modules/$(VirtoCommerce.ShopifyTaxonomy)/Scripts/blades/taxonomy-import.html',
            });
        }
    ]);
