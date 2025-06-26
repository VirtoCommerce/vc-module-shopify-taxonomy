angular.module('VirtoCommerce.ShopifyTaxonomy')
    .controller('VirtoCommerce.ShopifyTaxonomy.helloWorldController', ['$scope', 'VirtoCommerce.ShopifyTaxonomy.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'ShopifyTaxonomy';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'ShopifyTaxonomy.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
