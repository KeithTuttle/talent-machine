<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Copy, Mail, MailCheck, Plus, Trash2, UserMinus, X } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { useScopeStore } from '@/stores/scope'
import { useStaffStore } from '@/stores/staff'
import StaffPicker from '@/components/StaffPicker.vue'
import { STAFF_ROLE_LABELS } from '@/types'
import type { ProductionStaff, StaffMember, TeamMember, TeamResponse } from '@/types'

const scope = useScopeStore()
const staff = useStaffStore()

const tab = ref<'team' | 'directory'>('team')
const team = ref<TeamResponse | null>(null)
const inviteName = ref('')
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

// Directory data
const allAssignments = ref<ProductionStaff[]>([])
async function loadDirectory() {
  await Promise.all([staff.fetch(true), scope.fetchAll().catch(() => {})])
  allAssignments.value = await api.get<ProductionStaff[]>('/productionstaff').then((r) => r.data).catch(() => [])
}

onMounted(() => {
  load()
  if (scope.productions.length === 0) scope.fetchAll().catch(() => {})
  loadDirectory()
})

// --- Invites -----------------------------------------------------------------

function onPickInvitee(member: StaffMember) {
  inviteName.value = member.name
  inviteEmail.value = member.email ?? ''
}

async function createInvite() {
  if (!inviteName.value.trim() || !inviteEmail.value.trim()) {
    toast.error('Enter a name and email for the invite.')
    return
  }
  const { data } = await api.post<{ emailSent: boolean }>('/team/invitations', {
    name: inviteName.value.trim(),
    email: inviteEmail.value.trim(),
    productionId: inviteProductionId.value === '' ? null : inviteProductionId.value,
  })
  toast.success(data.emailSent ? 'Invite emailed' : 'Invite created — copy the code to share it')
  inviteName.value = ''
  inviteEmail.value = ''
  inviteProductionId.value = ''
  staff.fetch(true)
  await load()
}

async function resendInvite(id: number) {
  try {
    await api.post(`/team/invitations/${id}/send`)
    toast.success('Invite re-sent')
    await load()
  } catch {
    /* interceptor toasts */
  }
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
  window.setTimeout(() => window.location.reload(), 800)
}

// --- Member access -----------------------------------------------------------

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

// --- Directory ---------------------------------------------------------------

const memberEmails = computed(
  () => new Set((team.value?.members ?? []).map((m) => (m.email ?? '').toLowerCase()).filter(Boolean)),
)

interface DirectoryRow {
  member: StaffMember
  assignments: { title: string; role: string }[]
  hasLogin: boolean
}
const directory = computed<DirectoryRow[]>(() =>
  [...staff.members]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((member) => ({
      member,
      assignments: allAssignments.value
        .filter((a) => a.staffMemberId === member.id)
        .map((a) => ({ title: productionTitle(a.productionId) ?? '', role: STAFF_ROLE_LABELS[a.role] })),
      hasLogin: !!member.email && memberEmails.value.has(member.email.toLowerCase()),
    })),
)
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-5 p-6">
    <div>
      <h1 class="font-display text-2xl font-bold">Team</h1>
      <p class="text-sm text-muted-foreground">
        {{ team ? `${team.tenantName} — you are ${team.yourRole}.` : 'Share your company with your creative team.' }}
        The Owner sees everything; members only see the shows they're granted.
      </p>
    </div>

    <div class="flex rounded-md border border-border text-sm">
      <button
        v-for="t in ([['team', 'Members & invites'], ['directory', 'Directory']] as const)"
        :key="t[0]"
        class="flex-1 px-3 py-1.5 font-medium"
        :class="tab === t[0] ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground'"
        @click="tab = t[0]"
      >
        {{ t[1] }}
      </button>
    </div>

    <template v-if="tab === 'team'">
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

            <div v-if="m.role === 'Member'" class="flex flex-wrap items-center gap-1.5">
              <span class="text-xs text-muted-foreground">Shows:</span>
              <span v-if="m.productionIds.length === 0" class="text-xs italic text-muted-foreground">none yet</span>
              <span
                v-for="pid in m.productionIds"
                :key="pid"
                class="flex items-center gap-1 rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground"
              >
                {{ productionTitle(pid) }}
                <button v-if="isOwner" class="hover:text-destructive" :aria-label="`Revoke access to ${productionTitle(pid)}`" @click="revokeAccess(m, pid)">
                  <X class="h-3 w-3" />
                </button>
              </span>
              <template v-if="isOwner && grantablesFor(m).length > 0">
                <select v-model="grantSelection[m.id]" class="rounded-md border border-border bg-background px-1.5 py-0.5 text-xs focus:outline-none">
                  <option value="">Grant show…</option>
                  <option v-for="p in grantablesFor(m)" :key="p.id" :value="p.id">{{ p.title }}</option>
                </select>
                <button class="rounded-md border border-border px-2 py-0.5 text-xs hover:bg-accent" @click="grantAccess(m)">Grant</button>
              </template>
            </div>
          </li>
        </ul>
      </section>

      <!-- Invitations -->
      <section v-if="isOwner" class="rounded-lg border border-border">
        <h2 class="border-b border-border px-4 py-3 text-sm font-semibold">Invitations</h2>
        <form class="space-y-2 p-4" @submit.prevent="createInvite">
          <StaffPicker placeholder="Returning person? Search the directory…" @select="onPickInvitee" />
          <div class="flex flex-wrap gap-2">
            <input
              v-model="inviteName"
              placeholder="Name"
              class="min-w-32 flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
            <input
              v-model="inviteEmail"
              type="email"
              placeholder="Email (an access email is sent here)"
              class="min-w-40 flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>
          <div class="flex flex-wrap gap-2">
            <select
              v-model="inviteProductionId"
              class="flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="">No show yet (grant later)</option>
              <option v-for="p in scope.productions" :key="p.id" :value="p.id">For: {{ p.title }}</option>
            </select>
            <button
              type="submit"
              class="flex items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
            >
              <Plus class="h-4 w-4" /> Invite &amp; email
            </button>
          </div>
        </form>
        <ul v-if="team && team.invitations.length > 0" class="divide-y divide-border border-t border-border">
          <li v-for="i in team.invitations" :key="i.id" class="flex items-center justify-between px-4 py-2.5 text-sm">
            <div class="flex flex-wrap items-center gap-2">
              <code class="rounded bg-muted px-2 py-0.5 font-mono text-sm tracking-widest">{{ i.code }}</code>
              <span class="text-xs text-muted-foreground">{{ i.name }} · {{ i.email }}</span>
              <span v-if="i.productionId != null" class="rounded-full bg-accent px-2 py-0.5 text-xs text-accent-foreground">
                {{ productionTitle(i.productionId) }}
              </span>
              <span v-if="i.emailSent" class="flex items-center gap-0.5 text-xs text-emerald-600 dark:text-emerald-400">
                <MailCheck class="h-3.5 w-3.5" /> sent
              </span>
            </div>
            <div class="flex items-center gap-1">
              <button
                v-if="i.canEmail"
                class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                :aria-label="i.emailSent ? 'Resend email' : 'Send email'"
                :title="i.emailSent ? 'Resend email' : 'Send email'"
                @click="resendInvite(i.id)"
              >
                <Mail class="h-4 w-4" />
              </button>
              <button class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground" aria-label="Copy code" title="Copy code" @click="copyCode(i.code)">
                <Copy class="h-4 w-4" />
              </button>
              <button class="rounded-md p-1.5 text-muted-foreground hover:bg-accent hover:text-destructive" aria-label="Revoke invite" title="Revoke invite" @click="revokeInvite(i.id)">
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
          <button type="submit" class="rounded-md border border-border px-3 py-1.5 text-sm font-medium hover:bg-accent">Join</button>
        </form>
        <p class="px-4 pb-4 text-xs text-muted-foreground">
          Joining moves your account into the inviting company; your current (empty) account is left behind.
        </p>
      </section>
    </template>

    <!-- Directory -->
    <section v-else class="rounded-lg border border-border">
      <h2 class="border-b border-border px-4 py-3 text-sm font-semibold">
        Directory
        <span class="ml-1 font-normal text-muted-foreground">— everyone who's worked with the company</span>
      </h2>
      <p v-if="directory.length === 0" class="p-4 text-sm text-muted-foreground">
        No people yet. Assign creative team on a show (Seasons &amp; Shows) or send an invite.
      </p>
      <ul v-else class="divide-y divide-border">
        <li v-for="row in directory" :key="row.member.id" class="px-4 py-3 text-sm">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">{{ row.member.name }}</span>
            <span v-if="row.hasLogin" class="rounded bg-accent px-1.5 py-0.5 text-xs text-accent-foreground">has login</span>
            <span v-if="row.member.email" class="text-xs text-muted-foreground">
              <a :href="`mailto:${row.member.email}`" class="text-primary hover:underline">{{ row.member.email }}</a>
            </span>
            <span v-if="row.member.phone" class="text-xs text-muted-foreground">· {{ row.member.phone }}</span>
          </div>
          <div v-if="row.assignments.length > 0" class="mt-1 flex flex-wrap gap-1.5">
            <span
              v-for="(a, idx) in row.assignments"
              :key="idx"
              class="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground"
            >
              {{ a.role }} · {{ a.title }}
            </span>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>
