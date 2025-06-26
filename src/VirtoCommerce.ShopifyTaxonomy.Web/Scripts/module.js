// Call this to register your module to main application
var moduleName = 'VirtoCommerce.ShopifyTaxonomy';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.ShopifyTaxonomyState', {
                    url: '/shopify-taxonomy',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'VirtoCommerce.ShopifyTaxonomy.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.ShopifyTaxonomy)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/shopify-taxonomy',
                icon: 'fa fa-cube',
                title: 'ShopifyTaxonomy',
                priority: 100,
                action: function () { $state.go('workspace.ShopifyTaxonomyState'); },
                permission: 'shopify-taxonomy:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
