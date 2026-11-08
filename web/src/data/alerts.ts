import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api/client'
import type { AlertChannelInput, ApiAlertChannel, ApiChannelCreated, ApiDelivery, ApiDeliveryPage } from '@/api/types'
import { CHANNEL_TYPE, deliveryFromApi, type ChannelType, type DeliveryView } from './incidentView'
import { useIncidentAccess } from './incidents'

export interface ChannelView {
  id: string
  type: ChannelType
  name: string
  recipients: string[]
  /** The host a webhook points at. The full address is a secret and is never shown. */
  destination: string | null
  monitorCount: number
  lastDelivery: DeliveryView | null
}

/** What a person fills in. A blank `url` on edit keeps the address already stored. */
export interface ChannelDraft {
  type: ChannelType
  name: string
  recipients: string[]
  url: string
}

export const channelKeys = {
  list: (scope: string) => ['alert-channels', scope] as const,
  deliveries: (id: string) => ['alert-channel', id, 'deliveries'] as const,
}

const channelFromApi = (c: ApiAlertChannel): ChannelView => ({
  id: c.id,
  type: CHANNEL_TYPE[c.type],
  name: c.name,
  recipients: c.recipients,
  destination: c.destination,
  monitorCount: c.monitorCount,
  lastDelivery: c.lastDelivery ? deliveryFromApi(c.lastDelivery) : null,
})

const DEMO_CHANNELS: ChannelView[] = [
  {
    id: 'demo-email',
    type: 'email',
    name: 'On-call engineers',
    recipients: ['oncall@pulse-demo.dev', 'sam@pulse-demo.dev'],
    destination: null,
    monitorCount: 6,
    lastDelivery: null,
  },
  {
    id: 'demo-slack',
    type: 'slack',
    name: '#ops-alerts',
    recipients: [],
    destination: 'hooks.slack.com',
    monitorCount: 8,
    lastDelivery: null,
  },
]

const toBody = (draft: ChannelDraft): AlertChannelInput => ({
  type: draft.type === 'email' ? 'Email' : draft.type === 'discord' ? 'Discord' : draft.type === 'slack' ? 'Slack' : 'Webhook',
  name: draft.name.trim(),
  ...(draft.type === 'email' ? { recipients: draft.recipients } : { url: draft.url.trim() }),
})

export function useChannels() {
  const { demoMode, orgId, scope, enabled } = useIncidentAccess()

  return useQuery<ChannelView[]>({
    queryKey: channelKeys.list(scope),
    enabled,
    queryFn: async () => {
      if (demoMode) return DEMO_CHANNELS
      return (await api.get<ApiAlertChannel[]>(`/api/orgs/${orgId}/alert-channels`)).map(channelFromApi)
    },
  })
}

export interface SavedChannel {
  channel: ChannelView
  /** Shown once, when a generic webhook is created or its secret is rotated. */
  signingSecret: string | null
}

const saved = (result: ApiChannelCreated): SavedChannel => ({ channel: channelFromApi(result.channel), signingSecret: result.signingSecret })

export function useSaveChannel(id?: string) {
  const client = useQueryClient()
  const { orgId } = useIncidentAccess()

  return useMutation({
    mutationFn: async (draft: ChannelDraft) =>
      saved(
        id
          ? await api.patch<ApiChannelCreated>(`/api/alert-channels/${id}`, toBody(draft))
          : await api.post<ApiChannelCreated>(`/api/orgs/${orgId}/alert-channels`, toBody(draft)),
      ),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['alert-channels'] }),
  })
}

export function useRotateSecret() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => saved(await api.post<ApiChannelCreated>(`/api/alert-channels/${id}/rotate-secret`)),
    onSuccess: () => void client.invalidateQueries({ queryKey: ['alert-channels'] }),
  })
}

export function useDeleteChannel() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.delete(`/api/alert-channels/${id}`),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['alert-channels'] })
      void client.invalidateQueries({ queryKey: ['monitors'] })
    },
  })
}

/** Sends a test alert now and says how it went. */
export function useTestChannel() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => deliveryFromApi(await api.post<ApiDelivery>(`/api/alert-channels/${id}/test`)),
    onSettled: (_data, _error, id) => {
      void client.invalidateQueries({ queryKey: channelKeys.deliveries(id) })
      void client.invalidateQueries({ queryKey: ['alert-channels'] })
    },
  })
}

/** What a channel has sent, newest first. */
export function useDeliveries(channelId: string | null) {
  const { demoMode } = useIncidentAccess()

  return useInfiniteQuery({
    queryKey: channelKeys.deliveries(channelId ?? ''),
    enabled: Boolean(channelId) && !demoMode,
    initialPageParam: null as string | null,
    getNextPageParam: (page: { items: DeliveryView[]; next: string | null }) => page.next,
    queryFn: async ({ pageParam }) => {
      const query = new URLSearchParams({ limit: '15' })
      if (pageParam) query.set('before', pageParam)
      const page = await api.get<ApiDeliveryPage>(`/api/alert-channels/${channelId}/deliveries?${query}`)
      return { items: page.items.map(deliveryFromApi), next: page.nextBefore }
    },
  })
}
