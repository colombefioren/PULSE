import type { HubConnection } from '@microsoft/signalr'

export interface LiveConnection {
  stop: () => void
}

export interface LiveHandlers {
  CheckRecorded: (data: unknown) => void
  MonitorChanged: (data: unknown) => void
  /** The socket came back after a drop: anything could have changed meanwhile. */
  Reconnected: () => void
}

/**
 * Opens the live socket for one team. The SignalR client is loaded on demand so it stays out of the first
 * page load. Cookies authenticate the socket; WebSockets only, because the negotiate request would need an
 * antiforgery token the browser cannot attach.
 */
export async function connectLive(orgId: string, handlers: LiveHandlers): Promise<LiveConnection> {
  const { HubConnectionBuilder, HttpTransportType, LogLevel } = await import('@microsoft/signalr')

  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/live', { skipNegotiation: true, transport: HttpTransportType.WebSockets })
    .withAutomaticReconnect([0, 2000, 5000, 10_000, 30_000])
    .configureLogging(LogLevel.None)
    .build()

  connection.on('CheckRecorded', handlers.CheckRecorded)
  connection.on('MonitorChanged', handlers.MonitorChanged)
  // Group membership is per connection, so every (re)connect has to subscribe again.
  connection.onreconnected(() => {
    void connection.invoke('Subscribe', orgId).then(handlers.Reconnected, () => undefined)
  })

  let stopped = false
  try {
    await connection.start()
    if (stopped) {
      void connection.stop()
    } else {
      await connection.invoke('Subscribe', orgId)
    }
  } catch {
    // No socket (blocked network, signed out, proxy without upgrade): screens fall back to slow polling.
  }

  return {
    stop: () => {
      stopped = true
      void connection.stop()
    },
  }
}
