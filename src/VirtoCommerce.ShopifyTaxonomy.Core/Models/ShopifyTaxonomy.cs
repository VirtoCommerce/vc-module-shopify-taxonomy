using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.ShopifyTaxonomy.Core.Models;

public class ShopifyTaxonomy
{
    [JsonProperty("verticals")]
    public List<ShopifyVertical> Verticals { get; set; }

    [JsonProperty("attributes")]
    public List<ShopifyAttribute> Attributes { get; set; }
}

public class ShopifyVertical
{
    [JsonProperty("categories")]
    public List<ShopifyCategory> Categories { get; set; }
}

public class ShopifyCategory
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("level")]
    public int Level { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("full_name")]
    public string FullName { get; set; }

    [JsonProperty("parent_id")]
    public string ParentId { get; set; }

    [JsonProperty("attributes")]
    public List<ShopifyCategoryAttribute> Attributes { get; set; }

    [JsonProperty("children")]
    public List<ShopifyCategoryChild> Children { get; set; }

    [JsonProperty("ancestors")]
    public List<ShopifyCategoryAncestor> Ancestors { get; set; }
}

public class ShopifyCategoryAttribute
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("handle")]
    public string Handle { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }
}

public class ShopifyCategoryChild
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

public class ShopifyCategoryAncestor
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

public class ShopifyAttribute
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("handle")]
    public string Handle { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("values")]
    public List<ShopifyAttributeValue> Values { get; set; }
}

public class ShopifyAttributeValue
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("handle")]
    public string Handle { get; set; }
}
