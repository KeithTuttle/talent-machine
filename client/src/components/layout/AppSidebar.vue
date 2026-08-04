<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useDark, useToggle } from '@vueuse/core'
import { useScopeStore } from '@/stores/scope'
import { useCompanyStore } from '@/stores/company'
import {
  LayoutDashboard,
  Drama,
  Users,
  CalendarClock,
  ListOrdered,
  Clapperboard,
  Package,
  CalendarOff,
  CalendarRange,
  UserPlus,
  Building2,
  Check,
  ChevronDown,
  Plus,
  LogIn,
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
const company = useCompanyStore()
const router = useRouter()
const collapsed = ref(false) // desktop icon-only mode
const authEnabled = !!import.meta.env.VITE_CLERK_PUBLISHABLE_KEY
const switcherOpen = ref(false)

async function switchTo(id: number) {
  switcherOpen.value = false
  await company.switchCompany(id)
}
async function newCompany() {
  switcherOpen.value = false
  const name = window.prompt('Name your new company:')
  if (name?.trim()) await company.createCompany(name.trim())
}
function goJoin() {
  switcherOpen.value = false
  router.push('/team')
  emit('close')
}

// Dark mode: toggles the `.dark` class Tailwind's darkMode:['class'] strategy
// looks for, and persists the choice to localStorage. Defaults to the OS
// preference (prefers-color-scheme) when nothing has been chosen yet.
const isDark = useDark()
const toggleDark = useToggle(isDark)

const nav = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/', label: 'Planner', icon: Drama },
  { to: '/show-order', label: 'Show Order', icon: ListOrdered },
  { to: '/script', label: 'Script', icon: Clapperboard },
  { to: '/props', label: 'Props', icon: Package },
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

    <!-- Company switcher (only with auth on; a user may belong to several) -->
    <div v-if="authEnabled && !collapsed" class="relative border-b border-border p-2">
      <button
        class="flex w-full items-center gap-2 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent"
        @click="switcherOpen = !switcherOpen"
      >
        <Building2 class="h-4 w-4 shrink-0 text-muted-foreground" />
        <span class="min-w-0 flex-1 truncate text-left font-medium">{{ company.activeCompany?.name ?? 'Company' }}</span>
        <ChevronDown class="h-4 w-4 shrink-0 text-muted-foreground" />
      </button>
      <template v-if="switcherOpen">
        <div class="fixed inset-0 z-40" @click="switcherOpen = false" />
        <div class="absolute left-2 right-2 top-full z-50 mt-1 rounded-md border border-border bg-background p-1 shadow-lg">
          <button
            v-for="c in company.companies"
            :key="c.tenantId"
            class="flex w-full items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent"
            @click="switchTo(c.tenantId)"
          >
            <Check class="h-3.5 w-3.5 shrink-0" :class="c.tenantId === company.activeCompanyId ? 'text-primary' : 'invisible'" />
            <span class="min-w-0 flex-1 truncate text-left">{{ c.name }}</span>
            <span class="shrink-0 text-xs text-muted-foreground">{{ c.role }}</span>
          </button>
          <div class="my-1 border-t border-border" />
          <button class="flex w-full items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent" @click="newCompany">
            <Plus class="h-3.5 w-3.5" /> New company
          </button>
          <button class="flex w-full items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent" @click="goJoin">
            <LogIn class="h-3.5 w-3.5" /> Join with a code…
          </button>
        </div>
      </template>
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
