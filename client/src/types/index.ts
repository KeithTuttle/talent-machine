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
  notes?: string | null
  isActive: boolean
  createdAt: string
}

export interface CastGroup {
  id: number
  productionId: number
  name: string
  orderIndex: number
}

export interface CastMembership {
  id: number
  productionId: number
  performerId: number
  castGroupId?: number | null
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
