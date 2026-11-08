import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { api, ApiError } from '@/api/client'
import type {
  ApiPublicIncident,
  ApiPublicStatus,
  ApiStatusComponent,
  ApiStatusPage,
  ComponentInput,
  StatusPageInput,
} from '@/api/types'
import { connectStatus } from '@/live/status'
import { demo } from './demo'
import { SEVERITY, STATUS } from './incidentView'
import { useIncidentAccess } from './incidents'
import type { DayUptime, Incident, StatusComponent, StatusPage } from './types'

/**
 * Status pages: the ones a team builds (signed in) and the public page a visitor reads (anonymous).
 * The demo workspace has one simulated page, `pulse-demo`; every other address is a real page on the API.
 */

const DEMO_SLUG = 'pulse-demo'
/** The page is cached on the server for 30 seconds and pushed over a socket; polling is only a safety net. */
const SAFETY_MS = 60_000
const DEMO_MS = 5000

export const statusPageKeys = {
  all: ['status-pages'] as const,
  list: (scope: string) => ['status-pages', scope] as const,
  one: (id: string) => ['status-page', id] as const,
  public: (slug: string) => ['public-status', slug] as const,
}

/** A page being edited, as the screens use it. */
export interface StatusPageView {
  id: string
  slug: string
  title: string
  accent: string
  footer: string
  showResponseTimes: boolean
  logoUrl: string | null
  subscriberCount: number
  components: ComponentView[]
}

export interface ComponentView {
  id: string
  name: string
  group: string
  monitorIds: string[]
}

const NO_GROUP = ''

const componentFromApi = (c: ApiStatusComponent): ComponentView => ({
  id: c.id,
  name: c.name,
  group: c.groupName ?? NO_GROUP,
  monitorIds: c.monitorIds,
})

const pageFromApi = (p: ApiStatusPage): StatusPageView => ({
  id: p.id,
  slug: p.slug,
  title: p.title,
  accent: p.accentColor,
  footer: p.footer,
  showResponseTimes: p.showResponseTimes,
  logoUrl: p.logoUrl,
  subscriberCount: p.subscriberCount,
  components: p.components.map(componentFromApi),
})

/** What a person fills in on the settings form. */
export interface StatusPageDraft {
  slug: string
  title: string
  accent: string
  footer: string
  showResponseTimes: boolean
}

const toInput = (draft: StatusPageDraft): StatusPageInput => ({
  slug: draft.slug.trim(),
  title: draft.title.trim(),
  accentColor: draft.accent.trim() || null,
  footer: draft.footer.trim() || null,
  showResponseTimes: draft.showResponseTimes,
})

export interface ComponentDraft {
  name: string
  group: string
  monitorIds: string[]
}

const toComponentInput = (draft: ComponentDraft): ComponentInput => ({
  name: draft.name.trim(),
  groupName: draft.group.trim() || null,
  monitorIds: draft.monitorIds,
})

/** The team's status pages with their components. Empty in the demo, which has one simulated page of its own. */
export function useStatusPages() {
  const { demoMode, orgId, scope } = useIncidentAccess()

  return useQuery<StatusPageView[]>({
    queryKey: statusPageKeys.list(scope),
    enabled: Boolean(orgId) && !demoMode,
    queryFn: async () => (await api.get<ApiStatusPage[]>(`/api/orgs/${orgId}/status-pages`)).map(pageFromApi),
  })
}

/** One page of the team's, or null when it does not exist. */
export function useStatusPageDetail(id: string) {
  const { orgId, demoMode } = useIncidentAccess()

  return useQuery<StatusPageView | null>({
    queryKey: statusPageKeys.one(id),
    enabled: Boolean(orgId) && !demoMode,
    queryFn: async () => {
      try {
        return pageFromApi(await api.get<ApiStatusPage>(`/api/status-pages/${id}`))
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })
}

function useRefresh() {
  const client = useQueryClient()
  return (page?: ApiStatusPage) => {
    if (page) client.setQueryData(statusPageKeys.one(page.id), pageFromApi(page))
    void client.invalidateQueries({ queryKey: statusPageKeys.all })
  }
}

export function useCreateStatusPage() {
  const { orgId } = useIncidentAccess()
  const refresh = useRefresh()

  return useMutation({
    mutationFn: async (draft: StatusPageDraft) => pageFromApi(await api.post<ApiStatusPage>(`/api/orgs/${orgId}/status-pages`, toInput(draft))),
    onSuccess: () => refresh(),
  })
}

export function useUpdateStatusPage(id: string) {
  const refresh = useRefresh()

  return useMutation({
    mutationFn: async (draft: StatusPageDraft) => {
      const page = await api.patch<ApiStatusPage>(`/api/status-pages/${id}`, toInput(draft))
      refresh(page)
      return pageFromApi(page)
    },
  })
}

export function useDeleteStatusPage() {
  const refresh = useRefresh()

  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/status-pages/${id}`),
    onSuccess: () => refresh(),
  })
}

export function useUploadLogo(id: string) {
  const refresh = useRefresh()

  return useMutation({
    mutationFn: async (file: File) => {
      const page = await api.putFile<ApiStatusPage>(`/api/status-pages/${id}/logo`, file)
      refresh(page)
      return pageFromApi(page)
    },
  })
}

export function useRemoveLogo(id: string) {
  const refresh = useRefresh()

  return useMutation({
    mutationFn: async () => {
      const page = await api.delete<ApiStatusPage>(`/api/status-pages/${id}/logo`)
      refresh(page)
      return pageFromApi(page)
    },
  })
}

/** Adds a component (no id) or changes one (with its id). */
export function useSaveComponent(pageId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, draft }: { id?: string; draft: ComponentDraft }) =>
      id
        ? api.patch(`/api/status-pages/${pageId}/components/${id}`, toComponentInput(draft))
        : api.post(`/api/status-pages/${pageId}/components`, toComponentInput(draft)),
    onSuccess: () => void client.invalidateQueries({ queryKey: statusPageKeys.all }),
  })
}

export function useDeleteComponent(pageId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/status-pages/${pageId}/components/${id}`),
    onSuccess: () => void client.invalidateQueries({ queryKey: statusPageKeys.all }),
  })
}

/** Saves a new order. The list on screen moves at once and goes back if the server refuses. */
export function useReorderComponents(pageId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (ids: string[]) => api.put(`/api/status-pages/${pageId}/components/order`, { componentIds: ids }),
    onMutate: async (ids) => {
      await client.cancelQueries({ queryKey: statusPageKeys.one(pageId) })
      const previous = client.getQueryData<StatusPageView | null>(statusPageKeys.one(pageId))
      if (previous) {
        const byId = new Map(previous.components.map((c) => [c.id, c]))
        client.setQueryData<StatusPageView>(statusPageKeys.one(pageId), {
          ...previous,
          components: ids.map((id) => byId.get(id)).filter((c): c is ComponentView => Boolean(c)),
        })
      }
      return { previous }
    },
    onError: (_error, _ids, context) => {
      if (context?.previous) client.setQueryData(statusPageKeys.one(pageId), context.previous)
    },
    onSettled: () => void client.invalidateQueries({ queryKey: statusPageKeys.all }),
  })
}

// ---- The public page -------------------------------------------------------------------------------------------

const UNGROUPED = 'Services'

const publicIncident = (i: ApiPublicIncident): Incident => ({
  id: i.id,
  monitorId: null,
  title: i.title,
  severity: SEVERITY[i.severity],
  status: STATUS[i.status],
  startedAt: Date.parse(i.startedAt),
  acknowledgedAt: null,
  resolvedAt: i.resolvedAt ? Date.parse(i.resolvedAt) : null,
  isManual: false,
  updates: i.updates.map((u) => ({ status: STATUS[u.status], message: u.message, at: Date.parse(u.at) })),
  affects: i.components,
})

/** Turns the public API's page into the shape the page view draws, the same one the demo page uses. */
export function publicPageFromApi(p: ApiPublicStatus): StatusPage {
  return {
    slug: p.slug,
    title: p.title,
    accent: p.accentColor,
    footer: p.footer,
    showResponseTimes: p.showResponseTimes,
    logoUrl: p.logoUrl,
    live: true,
    components: p.components.map(
      (c): StatusComponent => ({
        id: c.id,
        name: c.name,
        group: c.group ?? UNGROUPED,
        status: c.status,
        uptime90d: c.uptime90d,
        avgResponseMs: c.avgResponseMs,
        days: c.days.map((d): DayUptime => ({ date: d.date, uptimePct: d.uptimePct, downtimeS: d.downtimeSeconds, incidents: d.incidents })),
      }),
    ),
    activeIncidents: p.activeIncidents.map(publicIncident),
    history: p.history.map(publicIncident),
  }
}

/** The public page at /status/:slug, or null when there is none. Anyone can read it; no session is needed. */
export function usePublicStatus(slug: string) {
  const simulated = slug === DEMO_SLUG

  return useQuery<StatusPage | null>({
    queryKey: statusPageKeys.public(slug),
    refetchInterval: simulated ? DEMO_MS : SAFETY_MS,
    queryFn: async () => {
      if (simulated) return demo.statusPage(slug) ?? null
      try {
        return publicPageFromApi(await api.get<ApiPublicStatus>(`/api/public/status/${encodeURIComponent(slug)}`))
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
  })
}

/** Reloads the public page when the server says something about it changed. Not used by the simulated page. */
export function useStatusLive(slug: string) {
  const client = useQueryClient()

  useEffect(() => {
    if (slug === DEMO_SLUG || !slug) return
    let stop: (() => void) | undefined
    let closed = false
    void connectStatus(slug, () => void client.invalidateQueries({ queryKey: statusPageKeys.public(slug) })).then((connection) => {
      if (closed) connection.stop()
      else stop = connection.stop
    })
    return () => {
      closed = true
      stop?.()
    }
  }, [client, slug])
}

export interface SubscribeResult {
  email: string
}

/** Asks for emails about the page. The answer looks the same whether or not the address is already subscribed. */
export function useSubscribe(slug: string) {
  return useMutation({
    mutationFn: async (email: string): Promise<SubscribeResult> => {
      if (slug === DEMO_SLUG) return { email }
      await api.postPublic(`/api/public/status/${encodeURIComponent(slug)}/subscribe`, { email })
      return { email }
    },
  })
}
