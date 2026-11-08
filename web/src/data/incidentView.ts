import type {
  ApiAffectedComponent,
  ApiAlertKind,
  ApiChannelType,
  ApiDelivery,
  ApiDeliveryStatus,
  ApiIncident,
  ApiIncidentDetail,
  ApiIncidentStatus,
  ApiSeverity,
  ApiUpdateKind,
} from '@/api/types'
import type { Incident, IncidentStatus, Severity } from './types'

/**
 * What the incident and alert screens render. The demo workspace and the API both map into these, so the
 * screens never know which one they are looking at.
 */
export interface IncidentView {
  id: string
  monitorId: string | null
  monitorName: string | null
  title: string
  severity: Severity
  status: IncidentStatus
  /** Epoch ms. */
  startedAt: number
  acknowledgedAt: number | null
  acknowledgedBy: string | null
  resolvedAt: number | null
  isManual: boolean
}

export type UpdateKind = 'opened' | 'update' | 'note' | 'acknowledged' | 'resolved'

export interface UpdateView {
  id: string
  kind: UpdateKind
  status: IncidentStatus
  message: string
  author: string | null
  at: number
}

export interface DeliveryView {
  id: string
  channelId: string
  channelName: string | null
  channelType: ChannelType | null
  incidentId: string | null
  kind: 'opened' | 'resolved' | 'test'
  status: 'pending' | 'sent' | 'failed'
  attempts: number
  createdAt: number
  sentAt: number | null
  error: string | null
}

export interface IncidentDetailView {
  incident: IncidentView
  updates: UpdateView[]
  alerts: DeliveryView[]
  /** The status page components the incident is shown on. */
  components: ApiAffectedComponent[]
}

export type ChannelType = 'email' | 'discord' | 'slack' | 'webhook'

export const SEVERITY: Record<ApiSeverity, Severity> = {
  Degraded: 'degraded',
  PartialOutage: 'partial',
  MajorOutage: 'major',
  Maintenance: 'maintenance',
}

export const API_SEVERITY: Record<Severity, ApiSeverity> = {
  degraded: 'Degraded',
  partial: 'PartialOutage',
  major: 'MajorOutage',
  maintenance: 'Maintenance',
}

export const STATUS: Record<ApiIncidentStatus, IncidentStatus> = {
  Investigating: 'investigating',
  Identified: 'identified',
  Monitoring: 'monitoring',
  Resolved: 'resolved',
}

export const API_STATUS: Record<IncidentStatus, ApiIncidentStatus> = {
  investigating: 'Investigating',
  identified: 'Identified',
  monitoring: 'Monitoring',
  resolved: 'Resolved',
}

const KIND: Record<ApiUpdateKind, UpdateKind> = {
  Opened: 'opened',
  Update: 'update',
  Note: 'note',
  Acknowledged: 'acknowledged',
  Resolved: 'resolved',
}

const DELIVERY_KIND: Record<ApiAlertKind, DeliveryView['kind']> = { IncidentOpened: 'opened', IncidentResolved: 'resolved', Test: 'test' }
const DELIVERY_STATUS: Record<ApiDeliveryStatus, DeliveryView['status']> = { Pending: 'pending', Sent: 'sent', Failed: 'failed' }
export const CHANNEL_TYPE: Record<ApiChannelType, ChannelType> = { Email: 'email', Discord: 'discord', Slack: 'slack', Webhook: 'webhook' }
export const API_CHANNEL_TYPE: Record<ChannelType, ApiChannelType> = { email: 'Email', discord: 'Discord', slack: 'Slack', webhook: 'Webhook' }

const time = (value: string | null) => (value ? Date.parse(value) : null)

export const incidentFromApi = (i: ApiIncident): IncidentView => ({
  id: i.id,
  monitorId: i.monitorId,
  monitorName: i.monitorName,
  title: i.title,
  severity: SEVERITY[i.severity],
  status: STATUS[i.status],
  startedAt: Date.parse(i.startedAt),
  acknowledgedAt: time(i.acknowledgedAt),
  acknowledgedBy: i.acknowledgedBy,
  resolvedAt: time(i.resolvedAt),
  isManual: i.isManual,
})

export const deliveryFromApi = (d: ApiDelivery): DeliveryView => ({
  id: d.id,
  channelId: d.channelId,
  channelName: d.channelName,
  channelType: d.channelType ? CHANNEL_TYPE[d.channelType] : null,
  incidentId: d.incidentId,
  kind: DELIVERY_KIND[d.kind],
  status: DELIVERY_STATUS[d.status],
  attempts: d.attempts,
  createdAt: Date.parse(d.createdAt),
  sentAt: time(d.sentAt),
  error: d.error,
})

export const detailFromApi = (detail: ApiIncidentDetail): IncidentDetailView => ({
  incident: incidentFromApi(detail.incident),
  updates: detail.updates.map((u) => ({
    id: u.id,
    kind: KIND[u.kind],
    status: STATUS[u.status],
    message: u.message,
    author: u.author,
    at: Date.parse(u.createdAt),
  })),
  alerts: detail.alerts.map(deliveryFromApi),
  components: detail.components ?? [],
})

/** Turns the demo workspace's incident into the shared shape. */
export function incidentFromDemo(incident: Incident, monitorName: string | null): IncidentView {
  return {
    id: incident.id,
    monitorId: incident.monitorId,
    monitorName,
    title: incident.title,
    severity: incident.severity,
    status: incident.status,
    startedAt: incident.startedAt,
    acknowledgedAt: incident.acknowledgedAt,
    acknowledgedBy: incident.acknowledgedAt ? 'You' : null,
    resolvedAt: incident.resolvedAt,
    isManual: incident.isManual,
  }
}

export function detailFromDemo(incident: Incident, monitorName: string | null): IncidentDetailView {
  const updates: UpdateView[] = incident.updates.map((update, index) => ({
    id: `${incident.id}-${index}`,
    kind: index === 0 && !incident.isManual ? 'opened' : update.status === 'resolved' ? 'resolved' : 'update',
    status: update.status,
    message: update.message,
    author: null,
    at: update.at,
  }))
  if (incident.acknowledgedAt) {
    updates.push({
      id: `${incident.id}-ack`,
      kind: 'acknowledged',
      status: incident.status,
      message: 'Acknowledged by You.',
      author: 'You',
      at: incident.acknowledgedAt,
    })
    updates.sort((a, b) => a.at - b.at)
  }
  return { incident: incidentFromDemo(incident, monitorName), updates, alerts: [], components: [] }
}

export const SEVERITY_LABEL: Record<Severity, string> = {
  degraded: 'Degraded',
  partial: 'Partial outage',
  major: 'Major outage',
  maintenance: 'Maintenance',
}

export const STATUS_LABEL: Record<IncidentStatus, string> = {
  investigating: 'Investigating',
  identified: 'Identified',
  monitoring: 'Monitoring',
  resolved: 'Resolved',
}

export const CHANNEL_LABEL: Record<ChannelType, string> = {
  email: 'Email',
  discord: 'Discord',
  slack: 'Slack',
  webhook: 'Webhook',
}
