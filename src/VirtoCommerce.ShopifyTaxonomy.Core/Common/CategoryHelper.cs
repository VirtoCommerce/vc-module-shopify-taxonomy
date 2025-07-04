using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Model;

namespace VirtoCommerce.ShopifyTaxonomy.Core.Common;

public static class CategoryHelper
{
    public static string FindClosestCommonAncestor(Dictionary<string, Category> categoryMap, string firstCategoryId, string secondCategoryId)
    {
        // Step 2: Get all ancestors (including self) for A and B
        var ancestorsOfA = GetAncestors(firstCategoryId, categoryMap);
        var pathToRootB = GetAncestorPath(secondCategoryId, categoryMap);

        // Step 3: Find the first ancestor in B's path that exists in A's ancestors
        foreach (var ancestor in pathToRootB)
        {
            if (ancestorsOfA.Contains(ancestor))
            {
                return ancestor; // This is the closest common ancestor
            }
        }

        return null; // No common ancestor found
    }

    private static HashSet<string> GetAncestors(string id, Dictionary<string, Category> categoryMap)
    {
        var ancestors = new HashSet<string>();
        while (id != null && categoryMap.TryGetValue(id, out var category))
        {
            ancestors.Add(id);
            id = category.ParentId;
        }
        return ancestors;
    }

    private static List<string> GetAncestorPath(string id, Dictionary<string, Category> categoryMap)
    {
        var path = new List<string>();
        while (id != null && categoryMap.TryGetValue(id, out var category))
        {
            path.Add(id);
            id = category.ParentId;
        }
        return path;
    }
}
