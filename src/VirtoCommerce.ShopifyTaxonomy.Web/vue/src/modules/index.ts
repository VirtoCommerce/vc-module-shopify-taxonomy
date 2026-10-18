import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, useExtensionPoint } from "@vc-shell/framework";
import { markRaw } from "vue";

// The Catalog list's toolbar extension point "catalog-items-toolbar" (contract in vc-module-catalog
// .../vue/src/modules/pages/browse.vue); only the fields used here are typed.
interface CatalogToolbarContext {
  catalog?: { id?: string; name?: string };
  openBlade: (event: { name: string; options?: Record<string, unknown> }) => Promise<void>;
}

// legacy: catalogImportService.register("Shopify Taxonomy Import") - an importer in the catalog's Import list
useExtensionPoint("catalog-items-toolbar").add({
  id: "shopify-taxonomy-import",
  priority: 30,
  component: markRaw({ render: () => null }),
  meta: {
    title: "SHOPIFY_TAXONOMY.IMPORT.COMMAND",
    icon: "lucide-list-tree",
    permissions: "catalog:import",
    isVisible: (ctx: CatalogToolbarContext) => !!ctx.catalog?.id,
    onClick: (ctx: CatalogToolbarContext) =>
      ctx.openBlade({ name: "ShopifyTaxonomyImport", options: { catalog: ctx.catalog } }),
  },
});

export default defineAppModule({
  blades,
  locales,
  // the import job's push notifications (legacy: notification template "ShopifyTaxonomyImport")
  notifications: {
    ShopifyTaxonomyImport: { toast: { mode: "progress" } },
  },
});

export * from "./pages";
