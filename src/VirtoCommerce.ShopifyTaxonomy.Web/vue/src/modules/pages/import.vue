<template>
  <VcBlade
    :title="$t('SHOPIFY_TAXONOMY.IMPORT.TITLE')"
    :subtitle="options?.catalog?.name"
    :toolbar-items="bladeToolbar"
    :loading="starting"
    width="50%"
  >
    <VcContainer>
      <VcForm class="shopify-form">
        <VcSwitch
          v-model="importProperties"
          :label="$t('SHOPIFY_TAXONOMY.IMPORT.PROPERTIES')"
          :disabled="running"
        />
        <VcSwitch
          v-model="importLocalizations"
          :label="$t('SHOPIFY_TAXONOMY.IMPORT.LOCALIZATIONS')"
          :disabled="running"
        />

        <!-- legacy: the "Importing Shopify Taxonomy" progress blade, here in place -->
        <VcCard
          v-if="job"
          :header="$t('SHOPIFY_TAXONOMY.PROGRESS.TITLE')"
        >
          <div class="shopify-job">
            <div class="shopify-job__description">{{ job.description }}</div>
            <VcProgress
              :value="percent"
              :variant="job.finished ? 'default' : 'striped'"
            />
            <div class="shopify-job__facts">
              <span v-if="job.created">{{ $t("SHOPIFY_TAXONOMY.PROGRESS.START") }}: {{ time(job.created) }}</span>
              <span v-if="job.finished">{{ $t("SHOPIFY_TAXONOMY.PROGRESS.END") }}: {{ time(job.finished) }}</span>
              <span>{{ $t("SHOPIFY_TAXONOMY.PROGRESS.TOTAL") }}: {{ job.totalCount ?? 0 }}</span>
              <span>{{ $t("SHOPIFY_TAXONOMY.PROGRESS.PROCESSED") }}: {{ job.processedCount ?? 0 }}</span>
              <span>{{ $t("SHOPIFY_TAXONOMY.PROGRESS.ERROR_COUNT") }}: {{ job.errorCount ?? job.errors?.length ?? 0 }}</span>
            </div>
            <VcBanner
              v-if="job.errors?.length"
              variant="danger"
              icon="lucide-circle-alert"
            >
              <div class="shopify-job__errors">
                <div
                  v-for="(e, i) in job.errors"
                  :key="i"
                >
                  {{ e }}
                </div>
              </div>
            </VcBanner>
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
// taxonomy-import (catalog "Import" → Shopify Taxonomy Import): import Shopify's standard product taxonomy into the
// browsed catalog as categories (+ properties, + localized names); progress from the job's push notifications
import { useApiClient, useAsync, useBlade, useBladeNotifications, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcCard, VcContainer, VcForm, VcProgress, VcSwitch } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  ShopifyTaxonomyClient,
  type ShopifyTaxonomyImportNotification,
} from "../../api_client/virtocommerce.shopifytaxonomy";

defineBlade({
  url: "/shopify-taxonomy-import",
  name: "ShopifyTaxonomyImport",
  permissions: ["catalog:import"],
  // the catalog comes only in options, which a reload doesn't bring back
  routable: false,
});

const { t } = useI18n({ useScope: "global" });
const { options, callParent } = useBlade<{ catalog: { id?: string; name?: string } }>();
const { getApiClient } = useApiClient(ShopifyTaxonomyClient);

const importProperties = ref(true);
const importLocalizations = ref(true);
const job = ref<ShopifyTaxonomyImportNotification>();
const running = computed(() => !!job.value && !job.value.finished);
const percent = computed(() => {
  if (job.value?.finished) return 100;
  return job.value?.totalCount ? Math.round(((job.value.processedCount ?? 0) * 100) / job.value.totalCount) : 0;
});
const time = (d: Date | string) => new Date(d).toLocaleTimeString();

useBladeNotifications<ShopifyTaxonomyImportNotification>({
  types: ["ShopifyTaxonomyImport"],
  filter: (msg) => msg.id === job.value?.id,
  onMessage(msg) {
    job.value = { ...job.value, ...msg };
    // the catalog list shows the imported categories
    if (msg.finished) callParent("reload");
  },
});

const { loading: starting, action: start } = useAsync(async () => {
  job.value = await (
    await getApiClient()
  ).doImport({
    catalogId: options.value?.catalog?.id,
    importProperties: importProperties.value,
    importLocalizations: importLocalizations.value,
  });
});

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "start",
    title: t("SHOPIFY_TAXONOMY.IMPORT.START"),
    icon: "lucide-play",
    // without a catalog the server skips its check and queues a job that fails
    disabled: computed(() => running.value || starting.value || !options.value?.catalog?.id),
    clickHandler: () => start(),
  },
]);
</script>

<style lang="scss">
.shopify-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.shopify-job {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;

  &__facts {
    display: flex;
    flex-wrap: wrap;
    gap: 6px 20px;
    font-size: 13px;
    color: var(--color-neutral-500);
  }

  &__errors {
    max-height: 240px;
    overflow: auto;
    white-space: pre-wrap;
    word-break: break-word;
  }
}
</style>
