export type Role = 'Owner' | 'Admin' | 'Viewer'

export interface OrgSummary {
  id: string
  name: string
  slug: string
  role: Role
}

export interface OrgDetail extends OrgSummary {
  createdAt: string
  memberCount: number
}

export interface Me {
  id: string
  email: string
  name: string
  emailConfirmed: boolean
  organizations: OrgSummary[]
}

export interface Member {
  userId: string
  email: string
  name: string
  role: Role
  joinedAt: string
}

export interface Invite {
  id: string
  email: string
  role: Role
  expiresAt: string
  createdAt: string
}

export interface InvitePreview {
  organizationName: string
  email: string
  role: Role
}

export interface RegisterInput {
  name: string
  teamName: string
  email: string
  password: string
}
