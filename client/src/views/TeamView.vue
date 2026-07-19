<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Copy, Plus, Trash2, UserMinus, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import type { TeamMember, TeamResponse } from '@/types'

const scope = useScopeStore()
const team = ref<TeamResponse | null>(null)
const inviteEmail = ref('')
const inviteProductionId = ref<number | ''>('')
const joinCode = ref('')
const grantSelection = ref<Record<number, number | ''>>({})

const isOwner = computed(() => (team.value?.yourRole ?? 'Owner') === 'Owner')
const productionTitle = (id?: number | null) =>
  scope.productions.find((p) => p.id === id)?.title ?? (id != null ? `Show #${id}` : null)

async function load() {
  team.value = await api.get<TeamResponse>('/team').then((r) => r.data).catch(() => null)
}
onMounted(() => {
  load()
  // The access pickers need the full show list (owners have it all).
  if (scope.productions.length === 0) scope.fetchAll().catch(() => {})
})

async function createInvite() {
  await api.post('/team/invitations', {
    email: inviteEmail.value.trim() || null,
    productionId: inviteProductionId.value === '' ? null : inviteProductionId.value,
  })
  inviteEmail.value = ''
  inviteProductionId.value = ''
  toast.success('Invite created — copy the code and share it')
  await load()
}

async function copyCode(code: string) {
  await navigator.clipboard.writeText(code)
  toast.success('Code copied')
}

async function revokeInvite(id: number) {
  await api.delete(`/team/invitations/${id}`)
  await load()
}

async function join() {
  const code = joinCode.value.trim()
  if (!code) return
  const { data } = await api.post<{ tenantName: string }>('/team/join', { code })
  toast.success(`Joined ${data.tenantName} — reloading…`)
  joinCode.value = ''
  // The whole app is scoped by tenant; reload so every store refetches.
  window.setTimeout(() => window.location.reload(), 800)
}

async function grantAccess(member: TeamMember) {
  const productionId = grantSelection.value[member.id]
  if (productionId === '' || productionId == null) return
  await api.post(`/team/members/${member.id}/access`, { productionId })
  grantSelection.value[member.id] = ''
  await load()
}

async function revokeAccess(member: TeamMember, productionId: number) {
  await api.delete(`/team/members/${member.id}/access/${productionId}`)
  await load()
}

/** Shows not yet granted to this member (for the grant picker). */
function grantablesFor(member: TeamMember) {
  return scope.productions.filter((p) => !member.productionIds.includes(p.id))
}

async function removeMember(id: number, name: string) {
  const ok = await confirm({
    title: `Remove ${name} from the team?`,
    message: 'Their next sign-in starts a fresh, empty account.',
    destructive: true,
    confirmText: 'Remove',
  })
  if (!ok) return
  await api.delete(`/team/members/${id}`)
  await load()
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6 p-6">
    <div>
      <h1 class="font-display text-2xl font-bold">Team</h1>
      <p class="text-sm text-muted-foreground">
        {{ team ? `${team.tenantName} — you are ${team.yourRole}.` : 'Share your company with your creative team.' }}
        The Owner sees everything; members only see the shows they're granted.
      </p>
    </div>

    <!-- Members -->
    <section class="rounded-lg border border-border">
      <h2 class="border-b border-border px-4 py-3 text-sm font-semibold">Members</h2>
      <p v-if="!team || team.members.length === 0" class="p-4 text-sm text-muted-foreground">
        No members to show (sign in with Clerk enabled to manage your team).
      </p>
      <ul v-else class="divide-y divide-border">
        <li v-for="m in team.members" :key="m.id" class="space-y-2 px-4 py-3 text-sm">
          <div class="flex items-center justify-between">
            <div>
              <span>{{ m.displayName ?? m.email ?? 'Member' }}</span>
              <span v-if="m.isYou" class="ml-2 rounded bg-accent px-1.5 py-0.5 text-xs text-accent-foreground">You</span>
              <span class="ml-2 text-xs text-muted-foreground">{{ m.role }}</span>
            </div>
            <button
              v-if="!m.isYou && isOwner"
              class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
              :aria-label="`Remove ${m.displayName ?? 'member'}`"
              @click="removeMember(m.id, m.displayName ?? m.email ?? 'this member')"
            >
              <UserMinus class="h-4 w-4" />
            </button>
          </div>

          <!-- Show-level access (members only; owners see everything) -->
          <div v-if="m.role === 'Member'" class="flex flex-wrap items-center gap-1.5">
            <span class="text-xs text-muted-foreground">Shows:</span>
            <span v-if="m.productionIds.length === 0" class="text-xs italic text-muted-foreground">
              none yet
            </span>
            <span
              v-for="pid in m.productionIds"
              :key="pid"
              class="flex items-center gap-1 rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground"
            >
              {{ productionTitle(pid) }}
              <button
                v-if="isOwner"
                class="hover:text-destructive"
                :aria-label="`Revoke access to ${productionTitle(pid)}`"
                @click="revokeAccess(m, pid)"
              >
                <X class="h-3 w-3" />
              </button>
            </span>
            <template v-if="isOwner && grantablesFor(m).length > 0">
              <select
                v-model="grantSelection[m.id]"
                class="rounded-md border border-border bg-background px-1.5 py-0.5 text-xs focus:outline-none"
              >
                <option value="">Grant show…</option>
                <option v-for="p in grantablesFor(m)" :key="p.id" :value="p.id">{{ p.title }}</option>
              </select>
              <button
                class="rounded-md border border-border px-2 py-0.5 text-xs hover:bg-accent"
                @click="grantAccess(m)"
              >
                Grant
              </button>
            </template>
          </div>
        </li>
      </ul>
    </section>

    <!-- Invitations -->
    <section v-if="isOwner" class="rounded-lg border border-border">
      <h2 class="border-b border-border px-4 py-3 text-sm font-semibold">Invitations</h2>
      <form class="flex flex-wrap gap-2 p-4" @submit.prevent="createInvite">
        <input
          v-model="inviteEmail"
          placeholder="Who is this for? (optional note)"
          class="min-w-40 flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
        <select
          v-model="inviteProductionId"
          class="rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        >
          <option value="">No show yet (grant later)</option>
          <option v-for="p in scope.productions" :key="p.id" :value="p.id">For: {{ p.title }}</option>
        </select>
        <button
          type="submit"
          class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
        >
          <Plus class="h-4 w-4" /> New invite
        </button>
      </form>
      <ul v-if="team && team.invitations.length > 0" class="divide-y divide-border border-t border-border">
        <li
          v-for="i in team.invitations"
          :key="i.id"
          class="flex items-center justify-between px-4 py-2.5 text-sm"
        >
          <div class="flex flex-wrap items-center gap-2">
            <code class="rounded bg-muted px-2 py-0.5 font-mono text-sm tracking-widest">{{ i.code }}</code>
            <span v-if="i.email" class="text-xs text-muted-foreground">for {{ i.email }}</span>
            <span
              v-if="i.productionId != null"
              class="rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground"
            >
              {{ productionTitle(i.productionId) }}
            </span>
          </div>
          <div class="flex items-center gap-1">
            <button
              class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              aria-label="Copy code"
              @click="copyCode(i.code)"
            >
              <Copy class="h-4 w-4" />
            </button>
            <button
              class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive"
              aria-label="Revoke invite"
              @click="revokeInvite(i.id)"
            >
              <Trash2 class="h-4 w-4" />
            </button>
          </div>
        </li>
      </ul>
    </section>

    <!-- Join -->
    <section class="rounded-lg border border-border">
      <h2 class="border-b border-border px-4 py-3 text-sm font-semibold">Join a team</h2>
      <form class="flex gap-2 p-4" @submit.prevent="join">
        <input
          v-model="joinCode"
          placeholder="Enter an invite code"
          class="flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 font-mono text-sm uppercase tracking-widest focus:outline-none focus:ring-1 focus:ring-ring"
        />
        <button
          type="submit"
          class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent"
        >
          Join
        </button>
      </form>
      <p class="px-4 pb-4 text-xs text-muted-foreground">
        Joining moves your account into the inviting company; your current (empty) account is left behind.
      </p>
    </section>
  </div>
</template>
