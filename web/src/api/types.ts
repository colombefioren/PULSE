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
  /** Channels that get an alert when this monitor goes down or recovers. */
  alertChannelIds?: string[]
}

export interface ApiMonitor extends Omit<MonitorInput, 'alertChannelIds'> {
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
  alertChannelIds: string[]
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

/* Incidents. */

export type ApiSeverity = 'Degraded' | 'PartialOutage' | 'MajorOutage' | 'Maintenance'
export type ApiIncidentStatus = 'Investigating' | 'Identified' | 'Monitoring' | 'Resolved'
export type ApiUpdateKind = 'Opened' | 'Update' | 'Note' | 'Acknowledged' | 'Resolved'

export interface ApiIncident {
  id: string
  organizationId: string
  monitorId: string | null
  monitorName: string | null
  title: string
  severity: ApiSeverity
  status: ApiIncidentStatus
  startedAt: string
  acknowledgedAt: string | null
  acknowledgedBy: string | null
  resolvedAt: string | null
  isManual: boolean
}

export interface ApiIncidentUpdate {
  id: string
  kind: ApiUpdateKind
  status: ApiIncidentStatus
  message: string
  author: string | null
  createdAt: string
}

/** A status page component an incident is shown on. */
export interface ApiAffectedComponent {
  id: string
  name: string
  statusPageId: string
  statusPageTitle: string
}

export interface ApiIncidentDetail {
  incident: ApiIncident
  updates: ApiIncidentUpdate[]
  alerts: ApiDelivery[]
  components: ApiAffectedComponent[]
}

export interface ApiIncidentPage {
  items: ApiIncident[]
  nextBefore: string | null
}

export interface ManualIncidentInput {
  title: string
  severity: ApiSeverity
  status: ApiIncidentStatus
  message: string
  monitorId?: string | null
  /** Status page components the incident is shown on. */
  componentIds?: string[]
}

export interface ApiSummary {
  up: number
  down: number
  paused: number
  pending: number
  openIncidents: number
  uptime24h: number | null
  uptime7d: number | null
  uptime30d: number | null
  avgResponseMs: number | null
}

export interface ApiOrgSeries {
  range: string
  series: ApiSeriesPoint[]
}

/* Alerts. */

export type ApiChannelType = 'Email' | 'Discord' | 'Slack' | 'Webhook'
export type ApiDeliveryStatus = 'Pending' | 'Sent' | 'Failed'
export type ApiAlertKind = 'IncidentOpened' | 'IncidentResolved' | 'Test'

export interface ApiDelivery {
  id: string
  channelId: string
  channelName: string | null
  channelType: ApiChannelType | null
  incidentId: string | null
  kind: ApiAlertKind
  status: ApiDeliveryStatus
  attempts: number
  createdAt: string
  sentAt: string | null
  error: string | null
}

export interface ApiAlertChannel {
  id: string
  organizationId: string
  type: ApiChannelType
  name: string
  recipients: string[]
  destination: string | null
  monitorCount: number
  lastDelivery: ApiDelivery | null
  createdAt: string
}

export interface ApiChannelCreated {
  channel: ApiAlertChannel
  signingSecret: string | null
}

export interface AlertChannelInput {
  type: ApiChannelType
  name: string
  recipients?: string[]
  url?: string
}

export interface ApiDeliveryPage {
  items: ApiDelivery[]
  nextBefore: string | null
}

/** What a person sets on a status page. A blank accent means the default. */
export interface StatusPageInput {
  slug: string
  title: string
  accentColor: string | null
  footer: string | null
  showResponseTimes: boolean
}

export interface ComponentInput {
  name: string
  groupName: string | null
  monitorIds: string[]
}

export interface ApiStatusComponent {
  id: string
  name: string
  groupName: string | null
  sortOrder: number
  monitorIds: string[]
}

export interface ApiStatusPage {
  id: string
  organizationId: string
  slug: string
  title: string
  accentColor: string
  footer: string
  showResponseTimes: boolean
  logoUrl: string | null
  subscriberCount: number
  components: ApiStatusComponent[]
  createdAt: string
}

export type ApiComponentStatus = 'up' | 'degraded' | 'down' | 'maintenance'

export interface ApiPublicDay {
  date: string
  uptimePct: number | null
  downtimeSeconds: number
  incidents: number
}

export interface ApiPublicComponent {
  id: string
  name: string
  group: string | null
  status: ApiComponentStatus
  uptime90d: number | null
  avgResponseMs: number | null
  days: ApiPublicDay[]
}

export interface ApiPublicUpdate {
  status: ApiIncidentStatus
  message: string
  at: string
}

export interface ApiPublicIncident {
  id: string
  title: string
  severity: ApiSeverity
  status: ApiIncidentStatus
  startedAt: string
  resolvedAt: string | null
  components: string[]
  updates: ApiPublicUpdate[]
}

export interface ApiPublicStatus {
  slug: string
  title: string
  logoUrl: string | null
  accentColor: string
  footer: string
  showResponseTimes: boolean
  generatedAt: string
  components: ApiPublicComponent[]
  activeIncidents: ApiPublicIncident[]
  history: ApiPublicIncident[]
}
