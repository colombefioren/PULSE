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

/* Monitors. */

export type ApiMonitorState = 'Pending' | 'Up' | 'Down' | 'Paused'
export type ApiMethod = 'Get' | 'Head' | 'Post'

export interface MonitorInput {
  name: string
  url: string
  method: ApiMethod
  intervalSeconds: number
  timeoutMs: number
  expectedCodes: string | null
  keyword: string | null
  headers: Record<string, string>
  failureThreshold: number
  recoveryThreshold: number
}

export interface ApiMonitor extends MonitorInput {
  id: string
  organizationId: string
  expectedCodes: string
  state: ApiMonitorState
  stateChangedAt: string
  lastCheckedAt: string | null
  lastResponseMs: number | null
  lastStatusCode: number | null
  lastErrorType: string | null
  lastErrorMessage: string | null
  badgeEnabled: boolean
  createdAt: string
}

export interface ApiMonitorListItem {
  monitor: ApiMonitor
  uptime24h: number | null
  sparkline: (number | null)[]
}

export interface ApiSeriesPoint {
  at: string
  avgMs: number | null
  checks: number
  failures: number
}

export interface ApiMonitorStats {
  range: string
  checks: number
  uptimePercent: number | null
  avgMs: number | null
  p50Ms: number | null
  p95Ms: number | null
  p99Ms: number | null
  uptime: { last24Hours: number | null; last7Days: number | null; last30Days: number | null }
  series: ApiSeriesPoint[]
}

export interface ApiCheck {
  checkedAt: string
  success: boolean
  statusCode: number | null
  responseMs: number
  errorType: string | null
  errorMessage: string | null
}

export interface ApiCheckPage {
  items: ApiCheck[]
  nextBefore: string | null
}
