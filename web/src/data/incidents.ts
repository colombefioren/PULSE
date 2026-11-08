import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/api/client'
import type {
  ApiIncident,
  ApiIncidentDetail,
  ApiIncidentPage,
  ApiOrgSeries,
  ApiSummary,
  ManualIncidentInput,
} from '@/api/types'
import type { SeriesPoint } from '@/lib/stats'
import { demo } from './demo'
import {
  API_SEVERITY,
  API_STATUS,
  detailFromApi,
  detailFromDemo,
  incidentFromApi,
  incidentFromDemo,
  type IncidentDetailView,
  type IncidentView,
} from './incidentView'
import { useMonitorAccess } from './monitors'
import type { ChartRange, IncidentStatus, Severity } from './types'

/**
 * Incidents and the dashboard numbers. The demo workspace serves them from its in-browser simulation (read-only
 * apart from acknowledging); a signed-in user gets them from the API.
 */

const LIVE_MS = 5000
/** The API pushes changes over a socket; polling is only a slow safety net. */
const SAFETY_MS = 60_000
const PAGE = 20

export type IncidentState = 'open' | 'resolved' | 'all'

export const incidentKeys = {
  all: ['incidents'] as const,
  list: (scope: string, state: IncidentState, monitorId?: string) => ['incidents', scope, state, monitorId ?? ''] as const,
  one: (id: string) => ['incident', id] as const,
  summary: (scope: string) => ['summary', scope] as const,
  series: (scope: string, range: ChartRange) => ['org-series', scope, range] as const,
}

/** Where this session's incidents live, and what it may do with them. */
export function useIncidentAccess() {
  const access = useMonitorAccess()
  const scope = access.demoMode ? 'demo' : (access.orgId ?? '')
  return { ...access, scope, enabled: access.demoMode || Boolean(access.orgId) }
}

function demoMonitorName(id: string | null): string | null {
  return id ? (demo.getMonitor(id)?.name ?? null) : null
}

/** Open incidents, or resolved ones, or both (newest first, a page at a time). Pass a monitor to see only its incidents. */
export function useIncidentList(state: IncidentState, monitorId?: string) {
  const { demoMode, orgId, scope, enabled } = useIncidentAccess()

  return useInfiniteQuery({
    queryKey: incidentKeys.list(scope, state, monitorId),
    enabled,
    initialPageParam: null as string | null,
    getNextPageParam: (page: { items: IncidentView[]; next: string | null }) => page.next,
    refetchInterval: demoMode ? LIVE_MS : SAFETY_MS,
    queryFn: async ({ pageParam }): Promise<{ items: IncidentView[]; next: string | null }> => {
      if (demoMode) {
        const items = demo
          .listIncidents()
          .filter((incident) => state === 'all' || (incident.status === 'resolved') === (state === 'resolved'))
          .filter((incident) => !monitorId || incident.monitorId === monitorId)
          .map((incident) => incidentFromDemo(incident, demoMonitorName(incident.monitorId)))
        return { items, next: null }
      }
      const query = new URLSearchParams({ state, limit: String(PAGE) })
      if (monitorId) query.set('monitorId', monitorId)
      if (pageParam) query.set('before', pageParam)
      const page = await api.get<ApiIncidentPage>(`/api/orgs/${orgId}/incidents?${query}`)
      return { items: page.items.map(incidentFromApi), next: page.nextBefore }
    },
  })
}

/** One incident with its timeline and the alerts it sent, or null when it does not exist. */
export function useIncidentDetail(id: string) {
  const { demoMode, enabled } = useIncidentAccess()

  return useQuery<IncidentDetailView | null>({
    queryKey: incidentKeys.one(id),
    enabled,
    refetchInterval: demoMode ? LIVE_MS : SAFETY_MS,
    queryFn: async () => {
      if (demoMode) {
        const incident = demo.listIncidents().find((i) => i.id === id)
        return incident ? detailFromDemo(incident, demoMonitorName(incident.monitorId)) : null
      }
      try {
        return detailFromApi(await api.get<ApiIncidentDetail>(`/api/incidents/${id}`))
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })
}

function refresh(client: ReturnType<typeof useQueryClient>, id?: string) {
  void client.invalidateQueries({ queryKey: incidentKeys.all })
  if (id) void client.invalidateQueries({ queryKey: incidentKeys.one(id), exact: true })
  void client.invalidateQueries({ queryKey: ['summary'] })
}

export function useAcknowledge() {
  const client = useQueryClient()
  const { demoMode } = useIncidentAccess()

  return useMutation({
    mutationFn: async (id: string) => {
      if (demoMode) {
        demo.acknowledge(id)
        return
      }
      await api.post<ApiIncidentDetail>(`/api/incidents/${id}/acknowledge`)
    },
    onSettled: (_data, _error, id) => refresh(client, id),
  })
}

export interface UpdateInput {
  message: string
  /** Set for a public status update on a manual incident; leave out for an internal note. */
  status?: IncidentStatus
}

export function useAddUpdate(id: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: ({ message, status }: UpdateInput) =>
      api.post<ApiIncidentDetail>(`/api/incidents/${id}/updates`, { message, status: status ? API_STATUS[status] : null }),
    onSuccess: (detail) => {
      client.setQueryData(incidentKeys.one(id), detailFromApi(detail))
      refresh(client)
    },
  })
}

export interface DeclareInput {
  title: string
  severity: Severity
  status: IncidentStatus
  message: string
  monitorId?: string | null
}

export function useDeclareIncident() {
  const client = useQueryClient()
  const { orgId } = useIncidentAccess()

  return useMutation({
    mutationFn: (input: DeclareInput) => {
      const body: ManualIncidentInput = {
        title: input.title,
        severity: API_SEVERITY[input.severity],
        status: API_STATUS[input.status],
        message: input.message,
        monitorId: input.monitorId ?? null,
      }
      return api.post<ApiIncidentDetail>(`/api/orgs/${orgId}/incidents`, body)
    },
    onSuccess: (detail) => {
      client.setQueryData(incidentKeys.one(detail.incident.id), detailFromApi(detail))
      refresh(client)
    },
  })
}

export function useEditIncident(id: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { title: string; severity: Severity }) =>
      api.patch<ApiIncident>(`/api/incidents/${id}`, { title: input.title, severity: API_SEVERITY[input.severity] }),
    onSuccess: () => refresh(client, id),
  })
}

export interface SummaryView {
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

/** The numbers at the top of the dashboard. */
export function useSummaryView() {
  const { demoMode, orgId, scope, enabled } = useIncidentAccess()

  return useQuery<SummaryView>({
    queryKey: incidentKeys.summary(scope),
    enabled,
    refetchInterval: demoMode ? LIVE_MS : SAFETY_MS,
    queryFn: async () => {
      if (demoMode) {
        demo.tickIfDue(LIVE_MS - 500)
        const s = demo.summary()
        const monitors = demo.listMonitors().filter((m) => m.status !== 'paused')
        const mean = (pick: (m: (typeof monitors)[number]) => number) =>
          monitors.length ? Math.round((monitors.reduce((sum, m) => sum + pick(m), 0) / monitors.length) * 100) / 100 : null
        return {
          up: s.up,
          down: s.down,
          paused: s.paused,
          pending: 0,
          openIncidents: s.openIncidents,
          uptime24h: s.uptime24h,
          uptime7d: mean((m) => m.uptime7d),
          uptime30d: mean((m) => m.uptime30d),
          avgResponseMs: s.avgResponseMs,
        }
      }
      return await api.get<ApiSummary>(`/api/orgs/${orgId}/summary`)
    },
  })
}

/** Average response time across every monitor. */
export function useOrgSeries(range: ChartRange) {
  const { demoMode, orgId, scope, enabled } = useIncidentAccess()

  return useQuery<SeriesPoint[]>({
    queryKey: incidentKeys.series(scope, range),
    enabled,
    staleTime: demoMode ? 60_000 : 15_000,
    refetchInterval: demoMode ? false : SAFETY_MS,
    queryFn: async () => {
      if (demoMode) return demo.globalSeries(range)
      const result = await api.get<ApiOrgSeries>(`/api/orgs/${orgId}/series?range=${range}`)
      return result.series.map((point) => ({ t: Date.parse(point.at), ms: point.avgMs }))
    },
  })
}
