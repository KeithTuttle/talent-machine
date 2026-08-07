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
  /** Opening date (yyyy-MM-dd); ages are computed as of this date. */
  openingDate?: string | null
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

export type TeachStatus = 'Taught' | 'NeedsReview' | 'Complete'

export interface MusicalNumber {
  id: number
  productionId: number
  title: string
  notes?: string | null
  /** Staff member choreographing this number; null = unassigned. */
  choreographerStaffId?: number | null
  /** Null = not taught yet. */
  teachStatus?: TeachStatus | null
  /** Null = not yet placed in the running order. */
  actId?: number | null
  /** Which scene this number sits inside; null = not nested under a scene. */
  sceneId?: number | null
  orderIndex: number
}

export interface NumberCast {
  musicalNumberId: number
  performerId: number
}

export interface Scene {
  id: number
  productionId: number
  /** Which act this scene belongs to; null = not yet placed. */
  actId?: number | null
  name: string
  setting?: string | null
  notes?: string | null
  orderIndex: number
}

/** A character (Role) present in a scene — the "who's in this scene" breakdown. */
export interface SceneCharacter {
  sceneId: number
  roleId: number
}

/** A character (Role) featured in a number — additive tag over NumberCast. */
export interface NumberCharacter {
  musicalNumberId: number
  roleId: number
}

/** A conflict Gemini extracted from a messy sheet (mirrors ConflictImportAiService). */
export interface AiConflictRow {
  performerName: string
  matchedName: string
  type: 'OneOff' | 'Weekly'
  startDate: string
  endDate: string
  weekdays: Weekday[]
  reason: string
}
export interface AiImportResult {
  configured: boolean
  ok: boolean
  rows: AiConflictRow[]
}

// Cast import (mirrors CastImportController / CastImportAiService).
export interface CastImportRow {
  existingPerformerId: number | null
  firstName: string
  lastName: string
  gender: Gender | null
  dateOfBirth: string | null
  notes: string | null
  castGroup: string | null
  guardianName: string | null
  guardianEmail: string | null
  guardianPhone: string | null
}
export interface CastImportSummary {
  performersCreated: number
  performersMatched: number
  addedToCast: number
  alreadyInCast: number
  guardiansCreated: number
  groupsCreated: number
}
export interface AiCastRow {
  firstName: string
  lastName: string
  gender: string
  dateOfBirth: string
  age: number | null
  notes: string
  guardianName: string
  guardianEmail: string
  guardianPhone: string
  castGroup: string
}
export interface AiCastResult {
  configured: boolean
  ok: boolean
  rows: AiCastRow[]
}

/** A company (tenant) the signed-in user belongs to (for the switcher). */
export interface Company {
  tenantId: number
  name: string
  role: string
  isActive: boolean
}

export type PropStatus = 'Needed' | 'Sourced' | 'Ready'
export const PROP_STATUSES: PropStatus[] = ['Needed', 'Sourced', 'Ready']

/** A production's prop-catalog entry (reusable across scenes). */
export interface Prop {
  id: number
  productionId: number
  name: string
  description?: string | null
  quantity: number
  storageLocation?: string | null
  status: PropStatus
  notes?: string | null
  orderIndex: number
}

/** A prop's use in one scene: preset, handler, strike-to, notes. */
export interface PropAssignment {
  id: number
  propId: number
  sceneId: number
  presetLocation?: string | null
  handler?: string | null
  strikeTo?: string | null
  notes?: string | null
}

// Dashboard aggregate (mirrors DashboardController's DTO records).
export interface DashboardCountdown {
  openingDate?: string | null
  daysToOpen?: number | null
}
export interface DashboardRollups {
  numbersTotal: number
  teachComplete: number
  teachTaught: number
  teachNeedsReview: number
  notTaught: number
  numbersWithCast: number
  rolesTotal: number
  rolesCast: number
  performersInShow: number
}
export interface DashboardWeekSlot {
  id: number
  date: string
  startTime: string
  endTime: string
  type: RehearsalType
  numberTitle?: string | null
  attendees: number
  conflicts: number
}
export interface DashboardAtRisk {
  performerId: number
  name: string
  present: number
  total: number
  percent: number
}
export interface DashboardAttendance {
  recordedSessions: number
  avgPercent: number
  atRisk: DashboardAtRisk[]
}
export interface DashboardCostumes {
  total: number
  ready: number
  sourced: number
  needed: number
  fittingsDone: number
  fittingsTotal: number
  /** Back-to-back changes — the ones that need a dresser. */
  quickChanges: number
}
export interface Dashboard {
  productionId: number
  title: string
  countdown: DashboardCountdown
  rollups: DashboardRollups
  thisWeek: DashboardWeekSlot[]
  attendance: DashboardAttendance
  costumes: DashboardCostumes
}

export interface Formation {
  id: number
  musicalNumberId: number
  formationName: string
  orderIndex: number
  /** JSON string: { [performerId]: { x, y } } in 0–100 percentages. */
  coordinates: string
}

export type CostumeStatus = 'Needed' | 'Sourced' | 'Ready'
export const COSTUME_STATUSES: CostumeStatus[] = ['Needed', 'Sourced', 'Ready']

/** A costume in the production's catalog — reused across numbers. */
export interface Costume {
  id: number
  productionId: number
  name: string
  description?: string | null
  accessories?: string | null
  shoes?: string | null
  photoUrl?: string | null
  vendorUrl?: string | null
  status: CostumeStatus
  notes?: string | null
  orderIndex: number
}

/** A catalog costume worn in a number. */
export interface CostumeNumber {
  costumeId: number
  musicalNumberId: number
}

/** Which costume a performer wears in a number. */
export interface CostumeAssignment {
  id: number
  musicalNumberId: number
  performerId: number
  /** Which catalog costume this performer wears; null = unassigned. */
  costumeId?: number | null
}

/**
 * One performer's fit in one costume — size, alteration notes, fitted. Keyed on
 * (costume, performer), so fitting a kid once counts everywhere it's worn.
 */
export interface CostumeFitting {
  costumeId: number
  performerId: number
  size?: string | null
  notes?: string | null
  isFitted: boolean
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

export interface StaffMember {
  id: number
  name: string
  email?: string | null
  phone?: string | null
  notes?: string | null
}

export type StaffRole = 'Director' | 'Choreographer' | 'MusicDirector' | 'Producer'
export const STAFF_ROLES: StaffRole[] = ['Director', 'Choreographer', 'MusicDirector', 'Producer']
export const STAFF_ROLE_LABELS: Record<StaffRole, string> = {
  Director: 'Director',
  Choreographer: 'Choreographer',
  MusicDirector: 'Music Director',
  Producer: 'Producer',
}

export interface ProductionStaff {
  productionId: number
  staffMemberId: number
  role: StaffRole
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
  /** Where the slot happens ("Studio A"); free text with a growing suggestion list. */
  room?: string | null
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
  name: string
  email: string
  role: string
  createdAt: string
  productionId?: number | null
  emailSent: boolean
  canEmail: boolean
}

export interface TeamResponse {
  tenantName: string
  yourRole: string
  members: TeamMember[]
  invitations: TeamInvitation[]
}
