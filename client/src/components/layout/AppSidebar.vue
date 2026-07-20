<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useDark, useToggle } from '@vueuse/core'
import { useScopeStore } from '@/stores/scope'
import {
  Drama,
  Users,
  CalendarClock,
  ListOrdered,
  CalendarOff,
  CalendarRange,
  UserPlus,
  ChevronsLeft,
  ChevronsRight,
  Sun,
  Moon,
  X,
} from 'lucide-vue-next'
import { UserButton } from '@clerk/vue'

defineProps<{ open?: boolean }>()
const emit = defineEmits<{ close: [] }>()

const scope = useScopeStore()
const collapsed = ref(false) // desktop icon-only mode
const authEnabled = !!import.meta.env.VITE_CLERK_PUBLISHABLE_KEY

// Dark mode: toggles the `.dark` class Tailwind's darkMode:['class'] strategy
// looks for, and persists the choice to localStorage. Defaults to the OS
// preference (prefers-color-scheme) when nothing has been chosen yet.
const isDark = useDark()
const toggleDark = useToggle(isDark)

const nav = [
  { to: '/', label: 'Planner', icon: Drama },
  { to: '/show-order', label: 'Show Order', icon: ListOrdered },
  { to: '/rehearsals', label: 'Rehearsals', icon: CalendarClock },
  { to: '/conflicts', label: 'Conflicts', icon: CalendarOff },
  { to: '/performers', label: 'Performers', icon: Users },
  { to: '/seasons', label: 'Seasons & Shows', icon: CalendarRange },
  { to: '/team', label: 'Team', icon: UserPlus },
]

function onSeasonChange(e: Event) {
  scope.selectSeason(Number((e.target as HTMLSelectElement).value))
}
function onProductionChange(e: Event) {
  scope.selectProduction(Number((e.target as HTMLSelectElement).value))
}
</script>

<template>
  <aside
    :class="[
      'fixed inset-y-0 left-0 z-40 flex h-full w-64 flex-col border-r border-border bg-background transition-transform duration-200',
      // Mobile: slide in/out. Desktop: always in flow, width toggles with collapse.
      'md:static md:z-auto md:translate-x-0 md:transition-all',
      open ? 'translate-x-0' : '-translate-x-full',
      collapsed ? 'md:w-16' : 'md:w-64',
    ]"
  >
    <!-- Brand + collapse/close -->
    <div class="flex min-h-14 items-center justify-between border-b border-border px-3 py-2">
      <RouterLink v-if="!collapsed" to="/" class="leading-tight">
        <span class="font-display block text-base font-bold text-primary">The Talent Machine</span>
        <span class="font-display block text-xs text-muted-foreground">Company Inc.</span>
      </RouterLink>
      <!-- Desktop collapse -->
      <button
        class="ml-auto hidden h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:flex"
        :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="collapsed = !collapsed"
      >
        <ChevronsRight v-if="collapsed" class="h-4 w-4" />
        <ChevronsLeft v-else class="h-4 w-4" />
      </button>
      <!-- Mobile close -->
      <button
        class="ml-auto flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:hidden"
        aria-label="Close menu"
        @click="emit('close')"
      >
        <X class="h-4 w-4" />
      </button>
    </div>

    <!-- Season + production pickers -->
    <div v-if="!collapsed" class="space-y-2 border-b border-border p-3">
      <label class="block space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Season</span>
        <select
          class="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          :value="scope.selectedSeasonId ?? ''"
          @change="onSeasonChange"
        >
          <option v-if="scope.seasons.length === 0" value="" disabled>No seasons yet</option>
          <option v-for="s in scope.seasons" :key="s.id" :value="s.id">
            {{ s.name || s.year }}
          </option>
        </select>
      </label>
      <label class="block space-y-1">
        <span class="text-xs font-medium text-muted-foreground">Production</span>
        <select
          class="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          :value="scope.selectedProductionId ?? ''"
          @change="onProductionChange"
        >
          <option v-if="scope.seasonProductions.length === 0" value="" disabled>
            No productions yet
          </option>
          <option v-for="p in scope.seasonProductions" :key="p.id" :value="p.id">
            {{ p.title }}
          </option>
        </select>
      </label>
    </div>

    <!-- Nav -->
    <nav class="flex-1 space-y-1 overflow-y-auto p-2">
      <RouterLink
        v-for="item in nav"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        active-class="bg-accent text-accent-foreground font-medium"
        :title="collapsed ? item.label : undefined"
        @click="emit('close')"
      >
        <component :is="item.icon" class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed" class="md:inline">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <!-- Footer: theme toggle + account -->
    <div class="space-y-1 border-t border-border p-2">
      <button
        class="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        :class="collapsed ? 'justify-center' : ''"
        :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
        :title="collapsed ? (isDark ? 'Light mode' : 'Dark mode') : undefined"
        @click="toggleDark()"
      >
        <Sun v-if="isDark" class="h-4 w-4 shrink-0" />
        <Moon v-else class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed">{{ isDark ? 'Light mode' : 'Dark mode' }}</span>
      </button>
      <div
        v-if="authEnabled"
        class="px-1 pt-1"
        :class="collapsed ? 'flex justify-center' : ''"
      >
        <UserButton :show-name="!collapsed" />
      </div>
    </div>
  </aside>
</template>
