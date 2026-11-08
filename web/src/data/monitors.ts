import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/api/client'
import type { ApiCheckPage, ApiMonitor, ApiMonitorListItem, ApiMonitorStats, MonitorInput } from '@/api/types'
import { summarize } from '@/lib/stats'
import { canManage, useSession } from '@/session/useSession'
import { demo } from './demo'
import {
  checkFromApi,
  listItemFromApi,
  monitorFromApi,
  monitorFromDemo,
  statsFromApi,
  type CheckView,
  type MonitorView,
  type StatsView,
} from './monitorView'
import type { ChartRange } from './types'

/**
 * Monitor data for the Monitors screens. In the demo workspace it comes from the in-browser simulation; for a
 * signed-in user it comes from the API. Both hand back the same `MonitorView`, so the screens do not care.
 */

const LIVE_MS = 5000
/** The API updates the screen over a socket; polling is only a slow safety net for when the socket is down. */
const SAFETY_MS = 60_000

export const monitorKeys = {
  list: (orgId: string) => ['monitors', orgId] as const,
  one: (id: string) => ['monitor', id] as const,
  stats: (id: string, range: ChartRange) => ['monitor', id, 'stats', range] as const,
  checks: (id: string) => ['monitor', id, 'checks'] as const,
}

/** What the current session may do with monitors, and where their data lives. */
export function useMonitorAccess() {
  const session = useSession()
  const ready = session.status === 'ready'
  const demoMode = ready && session.mode === 'demo'
  const orgId = ready && session.mode === 'user' ? (session.org?.id ?? null) : null
  const role = ready ? session.org?.role : undefined

  return {
    demoMode,
    orgId,
    /** Owners and Admins change monitors. The demo is read-only for create, edit and delete. */
    canEdit: ready && session.mode === 'user' && canManage(role),
    /** Pausing works for everyone who can edit, and in the demo (it is a simulation). */
    canPause: demoMode || (ready && canManage(role)),
  }
}

export function useMonitorList() {
  const { demoMode, orgId } = useMonitorAccess()

  return useQuery<MonitorView[]>({
    queryKey: monitorKeys.list(demoMode ? 'demo' : (orgId ?? '')),
    enabled: demoMode || Boolean(orgId),
    queryFn: async () => {
      if (demoMode) {
        demo.tickIfDue(LIVE_MS - 500)
        return demo.listMonitors().map(monitorFromDemo)
      }
      const now = Date.now()
      const items = await api.get<ApiMonitorListItem[]>(`/api/orgs/${orgId}/monitors`)
      return items.map((item) => listItemFromApi(item, now))
    },
    refetchInterval: demoMode ? LIVE_MS : SAFETY_MS,
  })
}

/** One monitor, or null when it does not exist (or belongs to a team the person is not in). */
export function useMonitorView(id: string) {
  const { demoMode, orgId } = useMonitorAccess()

  return useQuery<MonitorView | null>({
    queryKey: monitorKeys.one(id),
    enabled: demoMode || Boolean(orgId),
    queryFn: async () => {
      if (demoMode) {
        demo.tickIfDue(LIVE_MS - 500)
        const monitor = demo.getMonitor(id)
        return monitor ? monitorFromDemo(monitor) : null
      }
      try {
        return monitorFromApi(await api.get<ApiMonitor>(`/api/monitors/${id}`))
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    refetchInterval: demoMode ? LIVE_MS : SAFETY_MS,
  })
}

export function useMonitorStats(id: string, range: ChartRange) {
  const { demoMode, orgId } = useMonitorAccess()

  return useQuery<StatsView>({
    queryKey: monitorKeys.stats(id, range),
    enabled: demoMode || Boolean(orgId),
    placeholderData: keepPreviousData,
    staleTime: demoMode ? 60_000 : 15_000,
    queryFn: async () => {
      if (demoMode) {
        const series = demo.series(id, range)
        const summary = summarize(series)
        const monitor = demo.getMonitor(id)
        return {
          checks: summary.checks,
          uptimePct: summary.uptimePct,
          avgMs: summary.avgMs,
          p50: summary.p50,
          p95: summary.p95,
          p99: summary.p99,
          uptime24h: monitor?.uptime24h ?? null,
          uptime7d: monitor?.uptime7d ?? null,
          uptime30d: monitor?.uptime30d ?? null,
          uptime90d: null,
          series,
        }
      }
      return statsFromApi(await api.get<ApiMonitorStats>(`/api/monitors/${id}/stats?range=${range}`))
    },
  })
}

const PAGE = 25

/** Recent checks, newest first, loaded a page at a time. */
export function useMonitorChecks(id: string) {
  const { demoMode, orgId } = useMonitorAccess()

  return useInfiniteQuery({
    queryKey: monitorKeys.checks(id),
    enabled: demoMode || Boolean(orgId),
    initialPageParam: null as string | null,
    getNextPageParam: (page: { items: CheckView[]; next: string | null }) => page.next,
    queryFn: async ({ pageParam }): Promise<{ items: CheckView[]; next: string | null }> => {
      if (demoMode) {
        const items = [...demo.series(id, '1h')].reverse().map<CheckView>((point) => ({
          at: point.t,
          success: point.ms !== null,
          statusCode: point.ms === null ? null : 200,
          responseMs: point.ms ?? 10_000,
          message: point.ms === null ? "Timeout after 10s — the server didn't respond" : null,
        }))
        return { items: items.slice(0, 12), next: null }
      }
      const query = new URLSearchParams({ limit: String(PAGE) })
      if (pageParam) query.set('before', pageParam)
      const page = await api.get<ApiCheckPage>(`/api/monitors/${id}/checks?${query}`)
      return { items: page.items.map(checkFromApi), next: page.nextBefore }
    },
  })
}

/** Pause or resume. The row flips at once and goes back if the server says no. */
export function useSetPaused() {
  const client = useQueryClient()
  const { demoMode, orgId } = useMonitorAccess()
  const listKey = monitorKeys.list(demoMode ? 'demo' : (orgId ?? ''))

  return useMutation({
    mutationFn: async ({ id, paused }: { id: string; paused: boolean }) => {
      if (demoMode) {
        demo.setPaused(id, paused)
        return
      }
      await api.post(`/api/monitors/${id}/${paused ? 'pause' : 'resume'}`)
    },
    onMutate: async ({ id, paused }) => {
      await client.cancelQueries({ queryKey: listKey })
      await client.cancelQueries({ queryKey: monitorKeys.one(id), exact: true })
      const status = paused ? 'paused' : 'pending'
      const previousList = client.getQueryData<MonitorView[]>(listKey)
      const previousOne = client.getQueryData<MonitorView | null>(monitorKeys.one(id))
      client.setQueryData<MonitorView[]>(listKey, (list) => list?.map((m) => (m.id === id ? { ...m, status } : m)))
      client.setQueryData<MonitorView | null>(monitorKeys.one(id), (m) => (m ? { ...m, status } : m))
      return { previousList, previousOne }
    },
    onError: (_error, { id }, context) => {
      if (context?.previousList) client.setQueryData(listKey, context.previousList)
      if (context) client.setQueryData(monitorKeys.one(id), context.previousOne)
    },
    onSettled: (_data, _error, { id }) => {
      void client.invalidateQueries({ queryKey: listKey })
      void client.invalidateQueries({ queryKey: monitorKeys.one(id), exact: true })
      void client.invalidateQueries({ queryKey: ['summary'] })
    },
  })
}

/** Create a monitor, or replace the editable fields of an existing one. */
export function useSaveMonitor(id?: string) {
  const client = useQueryClient()
  const { orgId } = useMonitorAccess()

  return useMutation({
    mutationFn: (input: MonitorInput) =>
      id ? api.patch<ApiMonitor>(`/api/monitors/${id}`, input) : api.post<ApiMonitor>(`/api/orgs/${orgId}/monitors`, input),
    onSuccess: (saved) => {
      void client.invalidateQueries({ queryKey: monitorKeys.list(orgId ?? '') })
      client.setQueryData(monitorKeys.one(saved.id), monitorFromApi(saved))
      void client.invalidateQueries({ queryKey: monitorKeys.one(saved.id) })
    },
  })
}

export function useDeleteMonitor() {
  const client = useQueryClient()
  const { orgId } = useMonitorAccess()

  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/monitors/${id}`),
    onSuccess: (_data, id) => {
      client.removeQueries({ queryKey: monitorKeys.one(id) })
      void client.invalidateQueries({ queryKey: monitorKeys.list(orgId ?? '') })
    },
  })
}
