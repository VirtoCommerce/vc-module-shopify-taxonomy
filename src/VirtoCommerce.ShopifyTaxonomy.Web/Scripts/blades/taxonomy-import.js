angular.module('VirtoCommerce.ShopifyTaxonomy')
    .controller('VirtoCommerce.ShopifyTaxonomy.taxonomyImportController', ['$scope', 'VirtoCommerce.ShopifyTaxonomy.webApi', 'platformWebApp.bladeNavigationService',
        function ($scope, api, bladeNavigationService) {
            var blade = $scope.blade;
            blade.title = 'ShopifyTaxonomy.blades.import-blade.title';

            blade.refresh = function () {
                blade.importProperties = true;
                blade.importLocalizations = true;

                blade.isLoading = false;
            };

            $scope.startImport = function () {
                var request = {
                    catalogId: blade.catalog.id,
                    importProperties: blade.importProperties,
                    importLocalizations: blade.importLocalizations
                };
                api.importTaxonomy(request, function (notification) {
                    var newBlade = {
                        id: "taxonomyImportProgress",
                        catalog: blade.catalog,
                        notification: notification,
                        controller: 'VirtoCommerce.ShopifyTaxonomy.taxonomyImportProgressController',
                        template: 'Modules/$(VirtoCommerce.ShopifyTaxonomy)/Scripts/blades/taxonomy-import-progress.html'
                    };

                    $scope.$on("new-notification-event", function (event, notification) {
                        if (notification && notification.id == newBlade.notification.id) {
                            blade.canImport = notification.finished != null;
                        }
                    });

                    blade.canImport = false;
                    bladeNavigationService.showBlade(newBlade, blade.parentBlade);
                }, function (error) {
                    bladeNavigationService.setError('Error ' + error.status, blade);
                });
            };

            $scope.setForm = function (form) {
                $scope.formScope = form;
            };

            blade.refresh();
        }]);
