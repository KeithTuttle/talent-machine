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

export interface Person {
  id: number
  firstName: string
  lastName: string
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
  personId: number
  castGroupId?: number | null
  person?: Person | null
}

export interface Role {
  id: number
  productionId: number
  name: string
  personId?: number | null
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
  personId: number
}

// Team management (mirrors TeamController's DTO records).
export interface TeamMember {
  id: number
  role: string
  email?: string | null
  displayName?: string | null
  isYou: boolean
}

export interface TeamInvitation {
  id: number
  code: string
  email?: string | null
  role: string
  createdAt: string
}

export interface TeamResponse {
  tenantName: string
  yourRole: string
  members: TeamMember[]
  invitations: TeamInvitation[]
}
