angular.module('VirtoCommerce.ShopifyTaxonomy')
    .factory('VirtoCommerce.ShopifyTaxonomy.webApi', ['$resource', function ($resource) {
        return $resource('api/shopify-taxonomy');
    }]);
