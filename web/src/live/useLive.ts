import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useSession } from '@/session/useSession'
import {
  applyCheckRecorded,
  applyIncidentChanged,
  applyMonitorChanged,
  applyStatusChanged,
  type CheckRecordedEvent,
  type IncidentEvent,
  type MonitorChangedEvent,
} from './apply'
import { connectLive, type LiveConnection } from './connection'

/** Keeps the signed-in team's screens current. Mounted once, in the app shell. The demo has its own simulation. */
export function useLive() {
  const client = useQueryClient()
  const session = useSession()
  const orgId = session.status === 'ready' && session.mode === 'user' ? (session.org?.id ?? null) : null

  useEffect(() => {
    if (!orgId) return
    let live: LiveConnection | null = null
    let cancelled = false

    void connectLive(orgId, {
      CheckRecorded: (data) => applyCheckRecorded(client, orgId, data as CheckRecordedEvent),
      MonitorChanged: (data) => {
        applyMonitorChanged(client, orgId, data as MonitorChangedEvent)
        applyStatusChanged(client)
      },
      MonitorStatusChanged: () => applyStatusChanged(client),
      IncidentOpened: (data) => applyIncidentChanged(client, data as IncidentEvent),
      IncidentUpdated: (data) => applyIncidentChanged(client, data as IncidentEvent),
      IncidentResolved: (data) => applyIncidentChanged(client, data as IncidentEvent),
      Reconnected: () => {
        void client.invalidateQueries({ queryKey: ['monitors'] })
        void client.invalidateQueries({ queryKey: ['incidents'] })
        void client.invalidateQueries({ queryKey: ['summary'] })
      },
    }).then((connection) => {
      if (cancelled) connection.stop()
      else live = connection
    })

    return () => {
      cancelled = true
      live?.stop()
    }
  }, [client, orgId])
}
