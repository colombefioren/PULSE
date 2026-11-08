import type { HubConnection } from '@microsoft/signalr'
import type { LiveConnection } from './connection'

/**
 * Listens for changes to one public status page. Anyone can connect, no sign-in: the server only says
 * "something changed" and the page then fetches its (cached) public data again, so nothing private travels here.
 * The SignalR client is loaded on demand so it stays out of the first page load.
 */
export async function connectStatus(slug: string, onChange: () => void): Promise<LiveConnection> {
  const { HubConnectionBuilder, HttpTransportType, LogLevel } = await import('@microsoft/signalr')

  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/status', { skipNegotiation: true, transport: HttpTransportType.WebSockets })
    .withAutomaticReconnect([0, 2000, 5000, 10_000, 30_000])
    .configureLogging(LogLevel.None)
    .build()

  connection.on('StatusChanged', onChange)
  // Group membership is per connection, so every (re)connect has to subscribe again, and anything may have changed meanwhile.
  connection.onreconnected(() => {
    void connection.invoke('Subscribe', slug).then(onChange, () => undefined)
  })

  let stopped = false
  try {
    await connection.start()
    if (stopped) {
      void connection.stop()
    } else {
      await connection.invoke('Subscribe', slug)
    }
  } catch {
    // No socket (blocked network, proxy without upgrade): the page still refreshes by polling.
  }

  return {
    stop: () => {
      stopped = true
      void connection.stop()
    },
  }
}
