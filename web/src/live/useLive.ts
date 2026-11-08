import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useSession } from '@/session/useSession'
import { applyCheckRecorded, applyMonitorChanged, type CheckRecordedEvent, type MonitorChangedEvent } from './apply'
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
      MonitorChanged: (data) => applyMonitorChanged(client, orgId, data as MonitorChangedEvent),
      Reconnected: () => void client.invalidateQueries({ queryKey: ['monitors'] }),
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
