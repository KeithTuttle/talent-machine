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

export interface LevelGroup {
  id: number
  productionId: number
  name: string
  /** Free-text ability label, e.g. "Advanced". */
  level?: string | null
  minAge?: number | null
  maxAge?: number | null
  color?: string | null
  notes?: string | null
  orderIndex: number
}

export interface CastMembership {
  id: number
  productionId: number
  performerId: number
  castGroupId?: number | null
  levelGroupId?: number | null
  performer?: Performer | null
}

export interface Role {
  id: number
  productionId: number
  name: string
  performerId?: number | null
  notes?: string | null
  orderIndex: number
}

export interface MusicalNumber {
  id: number
  productionId: number
  title: string
  songwriter?: string | null
  notes?: string | null
  orderIndex: number
}

export interface NumberCast {
  musicalNumberId: number
  performerId: number
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
