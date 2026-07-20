// TypeScript mirrors of the API models (see server/TalentMachine.Api/Models).

export interface Season {
  id: number
  year: number
  name: string
  isArchived: boolean
  createdAt: string
}

export interface Production {
  id: number
  seasonId: number
  title: string
  /** Opening/performance date (yyyy-MM-dd); ages are computed as of this date. */
  showDate?: string | null
  notes?: string | null
  isArchived: boolean
  createdAt: string
}

export type Gender = 'Male' | 'Female' | 'NonBinary'

export interface Performer {
  id: number
  firstName: string
  lastName: string
  gender?: Gender | null
  /** yyyy-MM-dd; optional — when set the UI computes ages. */
  dateOfBirth?: string | null
  notes?: string | null
  isActive: boolean
  createdAt: string
}

export interface CastGroup {
  id: number
  productionId: number
  name: string
  /** Hex color for at-a-glance highlighting, e.g. "#EAB308". */
  color?: string | null
  orderIndex: number
}

export interface CastMembership {
  id: number
  productionId: number
  performerId: number
  castGroupId?: number | null
  /** Per-show notes (constant notes live on Performer.notes). */
  notes?: string | null
  performer?: Performer | null
}

export type ConflictType = 'OneOff' | 'Weekly'
export type Weekday =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export interface Conflict {
  id: number
  productionId: number
  performerId: number
  type: ConflictType
  /** yyyy-MM-dd. OneOff: range start. Weekly: recurrence start bound. */
  startDate: string
  /** OneOff: range end (null = single day). Weekly: recurrence end (null = open-ended). */
  endDate?: string | null
  /** Weekly only. */
  weekday?: Weekday | null
  reason?: string | null
}

export interface Role {
  id: number
  productionId: number
  name: string
  performerId?: number | null
  notes?: string | null
  orderIndex: number
}

export interface Act {
  id: number
  productionId: number
  name: string
  orderIndex: number
}

export interface MusicalNumber {
  id: number
  productionId: number
  title: string
  songwriter?: string | null
  notes?: string | null
  /** Null = not yet placed in the running order. */
  actId?: number | null
  orderIndex: number
}

export interface NumberCast {
  musicalNumberId: number
  performerId: number
}

export interface Guardian {
  id: number
  name: string
  email?: string | null
  phone?: string | null
  notes?: string | null
}

export interface PerformerGuardian {
  performerId: number
  guardianId: number
}

export type RehearsalType = 'Music' | 'Dance' | 'Blocking' | 'Runthrough' | 'Other'

export interface Rehearsal {
  id: number
  productionId: number
  /** yyyy-MM-dd */
  date: string
  /** HH:mm[:ss] */
  startTime: string
  endTime: string
  type: RehearsalType
  /** Null = general session; attendees come only from explicit overrides. */
  musicalNumberId?: number | null
  notes?: string | null
}

/** Override row: adds (isExcluded=false) or removes (true) a kid from a slot. */
export interface RehearsalAttendee {
  rehearsalId: number
  performerId: number
  isExcluded: boolean
}

export type AttendanceStatus = 'Present' | 'Absent' | 'Excused'

export interface RehearsalAttendance {
  rehearsalId: number
  performerId: number
  status: AttendanceStatus
}

export interface SuggestedSlot {
  date: string
  startTime: string
  endTime: string
  type: RehearsalType
  musicalNumberId?: number | null
  notes?: string | null
}

export interface SuggestResponse {
  configured: boolean
  ok: boolean
  slots: SuggestedSlot[]
}

// Team management (mirrors TeamController's DTO records).
export interface TeamMember {
  id: number
  role: string
  email?: string | null
  displayName?: string | null
  isYou: boolean
  /** Shows this member has been granted (empty for Owners — they see everything). */
  productionIds: number[]
}

export interface TeamInvitation {
  id: number
  code: string
  email?: string | null
  role: string
  createdAt: string
  productionId?: number | null
}

export interface TeamResponse {
  tenantName: string
  yourRole: string
  members: TeamMember[]
  invitations: TeamInvitation[]
}
