import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Me } from '@/api/types'
import { isDemoMode } from '@/session/demoMode'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const sharedDemo: Me = {
  id: 'user-demo',
  email: 'demo@pulse.invalid',
  name: 'Pulse Demo',
  emailConfirmed: true,
  organizations: [{ id: 'org-live', name: 'Pulse live demo', slug: 'pulse-live-demo', role: 'Viewer' }],
}

const summary = { up: 6, down: 0, paused: 0, pending: 0, openIncidents: 0, uptime24h: 100, uptime7d: 100, uptime30d: 100, avgResponseMs: 120 }

describe('Try the demo', () => {
  it('signs in to the shared demo account where the server has one', async () => {
    let signedIn = false
    const server = mockApi({
      'GET /api/auth/me': () => (signedIn ? { body: sharedDemo } : { status: 401, body: problem('not_signed_in', 'Sign in.') }),
      'POST /api/demo/session': () => {
        signedIn = true
        return { status: 204 }
      },
      'GET /api/orgs/org-live/summary': { body: summary },
    })
    const { router } = renderAt('/demo', { demo: false })

    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
    expect(server.called('POST /api/demo/session')).toHaveLength(1)
    expect(isDemoMode()).toBe(false)
    expect(await screen.findByRole('region', { name: 'Demo' })).toHaveTextContent('shared, read-only demo')
  })

  it('opens the built-in sample workspace where the server has no demo account', async () => {
    const server = mockApi({
      'GET /api/auth/me': { status: 401, body: problem('not_signed_in', 'Sign in.') },
      'POST /api/demo/session': { status: 404, body: problem('demo_unavailable', 'The shared demo is not turned on here.') },
    })
    const { router } = renderAt('/demo', { demo: false })

    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
    expect(server.called('POST /api/demo/session')).toHaveLength(1)
    expect(isDemoMode()).toBe(true)
    expect(screen.queryByRole('region', { name: 'Demo' })).not.toBeInTheDocument()
  })

  it('never replaces the session of someone who is already signed in', async () => {
    const server = mockApi({ 'GET /api/auth/me': { body: ana } })
    const { router } = renderAt('/demo', { demo: false })

    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
    expect(server.called('POST /api/demo/session')).toHaveLength(0)
    expect(isDemoMode()).toBe(true)
  })

  it('lets a visitor of the shared demo leave for their own team', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/auth/me': { body: sharedDemo },
      'GET /api/orgs/org-live/summary': { body: summary },
      'POST /api/auth/logout': { status: 204 },
    })
    const { router } = renderAt('/dashboard', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Create your own team' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/signup'))
    expect(server.called('POST /api/auth/logout')).toHaveLength(1)
  })
})
