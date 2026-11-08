import type { SeriesPoint } from '@/lib/stats'

export type MonitorStatus = 'up' | 'down' | 'paused' | 'pending'
export type Severity = 'degraded' | 'partial' | 'major' | 'maintenance'
export type IncidentStatus = 'investigating' | 'identified' | 'monitoring' | 'resolved'
export type ChartRange = '1h' | '24h' | '7d' | '30d'

export interface Monitor {
  id: string
  name: string
  url: string
  method: 'GET' | 'HEAD' | 'POST'
  intervalS: number
  status: MonitorStatus
  lastResponseMs: number | null
  /** Epoch ms of the most recent check. */
  lastCheckedAt: number
  uptime24h: number
  uptime7d: number
  uptime30d: number
  uptime90d: number
  /** Last 24 hours at 30 minute resolution, for the list sparkline. */
  spark: SeriesPoint[]
}

export interface IncidentUpdate {
  status: IncidentStatus
  message: string
  at: number
}

export interface Incident {
  id: string
  monitorId: string | null
  title: string
  severity: Severity
  status: IncidentStatus
  startedAt: number
  acknowledgedAt: number | null
  resolvedAt: number | null
  isManual: boolean
  updates: IncidentUpdate[]
}

export interface DayUptime {
  /** ISO date, UTC. */
  date: string
  /** Null when there was no data that day. */
  uptimePct: number | null
  downtimeS: number
  incidents: number
}

export interface StatusComponent {
  id: string
  name: string
  group: string
  status: 'up' | 'degraded' | 'down' | 'maintenance'
  uptime90d: number
  days: DayUptime[]
}

export interface StatusPage {
  slug: string
  title: string
  /** Customer accent color. Used for bars' highlights, never for text on white. */
  accent: string
  footer: string
  showResponseTimes: boolean
  components: StatusComponent[]
  activeIncidents: Incident[]
  history: Incident[]
}

export interface DashboardSummary {
  up: number
  down: number
  paused: number
  openIncidents: number
  uptime24h: number
  avgResponseMs: number
}
