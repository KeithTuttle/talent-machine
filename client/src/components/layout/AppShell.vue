<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Menu } from 'lucide-vue-next'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import Toaster from '@/components/ui/Toaster.vue'
import ConfirmDialog from '@/components/ui/ConfirmDialog.vue'
import { useScopeStore } from '@/stores/scope'

const scope = useScopeStore()
const route = useRoute()

// Off-canvas drawer state (mobile only; sidebar is persistent on md+).
const mobileNavOpen = ref(false)

onMounted(() => {
  // Best-effort: won't error the UI if the API isn't running yet.
  scope.fetchAll().catch(() => {})
})

// Close the drawer whenever navigation happens.
watch(() => route.fullPath, () => {
  mobileNavOpen.value = false
})
</script>

<template>
  <div class="flex h-screen w-full overflow-hidden bg-background text-foreground">
    <!-- Drawer backdrop (mobile) -->
    <div
      v-if="mobileNavOpen"
      class="fixed inset-0 z-30 bg-black/40 md:hidden"
      @click="mobileNavOpen = false"
    />

    <AppSidebar :open="mobileNavOpen" @close="mobileNavOpen = false" />

    <div class="flex min-w-0 flex-1 flex-col">
      <!-- Top bar (mobile only) -->
      <header
        class="flex h-14 shrink-0 items-center gap-3 border-b border-border px-4 md:hidden"
      >
        <button
          class="flex h-9 w-9 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          aria-label="Open menu"
          @click="mobileNavOpen = true"
        >
          <Menu class="h-5 w-5" />
        </button>
        <span class="font-display text-sm font-semibold tracking-tight">
          {{ (route.meta.title as string) ?? 'The Talent Machine Company' }}
        </span>
      </header>

      <main class="min-w-0 flex-1 overflow-y-auto">
        <RouterView />
      </main>
    </div>

    <!-- Global overlays -->
    <Toaster />
    <ConfirmDialog />
  </div>
</template>
