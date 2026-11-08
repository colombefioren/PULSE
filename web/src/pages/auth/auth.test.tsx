import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const anonymous = { 'GET /api/auth/me': { status: 401, body: problem('unauthenticated', 'Sign in first.') } }

describe('Sign in with a real account', () => {
  it('signs in, then lands on the page the person was heading for', async () => {
    const user = userEvent.setup()
    let signedIn = false
    const server = mockApi({
      'GET /api/auth/me': () => (signedIn ? { body: ana } : { status: 401, body: problem('unauthenticated', 'Sign in first.') }),
      'POST /api/auth/login': () => {
        signedIn = true
        return { status: 204 }
      },
    })
    const { router } = renderAt('/login?next=%2Fsettings', { demo: false })

    await user.type(await screen.findByLabelText('Email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Password'), 'correct horse battery')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/settings'))
    expect(server.called('POST /api/auth/login')[0]?.body).toMatchObject({ email: 'ana@example.com', password: 'correct horse battery' })
    expect(server.called('POST /api/auth/login')[0]?.csrf).toBe('test-token')
  })

  it('shows the server message for a wrong password and stays on the page', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'POST /api/auth/login': { status: 401, body: problem('invalid_credentials', 'Email or password is incorrect.') },
    })
    const { router } = renderAt('/login', { demo: false })

    await user.type(await screen.findByLabelText('Email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Password'), 'not the password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Email or password is incorrect.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('offers to resend the confirmation email when the address is not verified yet', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...anonymous,
      'POST /api/auth/login': { status: 403, body: problem('email_not_verified', 'Confirm your email before signing in.') },
      'POST /api/auth/resend-verification': { status: 202 },
    })
    renderAt('/login', { demo: false })

    await user.type(await screen.findByLabelText('Email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Password'), 'correct horse battery')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))
    await user.click(await screen.findByRole('button', { name: 'Send the confirmation email again' }))

    expect(await screen.findByText(/Check ana@example.com for a new link/)).toBeInTheDocument()
    expect(server.called('POST /api/auth/resend-verification')[0]?.body).toEqual({ email: 'ana@example.com' })
  })
})

describe('Sign up', () => {
  async function fill(user: ReturnType<typeof userEvent.setup>) {
    await user.type(await screen.findByLabelText('Your name'), 'Ana Rakoto')
    await user.type(screen.getByLabelText('Team name'), 'Acme Platform')
    await user.type(screen.getByLabelText('Work email'), 'ana@example.com')
    await user.type(screen.getByLabelText('Password'), 'correct horse battery')
    await user.click(screen.getByRole('button', { name: 'Create account' }))
  }

  it('asks the person to check their email once the account is created', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...anonymous, 'POST /api/auth/register': { status: 202 } })
    renderAt('/signup', { demo: false })

    await fill(user)

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument()
    expect(screen.getByText('ana@example.com')).toBeInTheDocument()
    expect(server.called('POST /api/auth/register')[0]?.body).toEqual({
      name: 'Ana Rakoto',
      teamName: 'Acme Platform',
      email: 'ana@example.com',
      password: 'correct horse battery',
    })
  })

  it('puts a server-side problem under the field it belongs to', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'POST /api/auth/register': { status: 400, body: problem('validation_failed', 'Check the form.', { teamName: ['That team name is taken.'] }) },
    })
    renderAt('/signup', { demo: false })

    await fill(user)

    const field = await screen.findByLabelText('Team name')
    expect(await screen.findByText('That team name is taken.')).toBeInTheDocument()
    expect(field).toHaveAccessibleDescription('That team name is taken.')
  })
})

describe('Email confirmation', () => {
  it('confirms the address from the emailed link', async () => {
    const server = mockApi({ ...anonymous, 'POST /api/auth/verify-email': { status: 204 } })
    renderAt('/verify-email?userId=u1&token=abc', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Email confirmed' })).toBeInTheDocument()
    expect(server.called('POST /api/auth/verify-email')).toHaveLength(1)
    expect(server.called('POST /api/auth/verify-email')[0]?.body).toEqual({ userId: 'u1', token: 'abc' })
  })

  it('explains a link that no longer works and offers a new one', async () => {
    mockApi({ ...anonymous, 'POST /api/auth/verify-email': { status: 400, body: problem('invalid_token', 'Invalid link.') } })
    renderAt('/verify-email?userId=u1&token=old', { demo: false })

    expect(await screen.findByRole('heading', { name: 'This link did not work' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send a new link' })).toBeInTheDocument()
  })

  it('does not call the API when the link is incomplete', async () => {
    const server = mockApi(anonymous)
    renderAt('/verify-email', { demo: false })

    expect(await screen.findByRole('heading', { name: 'This link did not work' })).toBeInTheDocument()
    expect(server.called('POST /api/auth/verify-email')).toHaveLength(0)
  })
})

describe('Password reset', () => {
  it('says the same thing whether or not the address has an account', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...anonymous, 'POST /api/auth/forgot-password': { status: 202 } })
    renderAt('/forgot-password', { demo: false })

    await user.type(await screen.findByLabelText('Email'), 'nobody@example.com')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByText(/has a Pulse account, a link to choose a new password is on its way/)).toBeInTheDocument()
    expect(server.called('POST /api/auth/forgot-password')[0]?.body).toEqual({ email: 'nobody@example.com' })
  })

  it('sets a new password from the emailed link', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...anonymous, 'POST /api/auth/reset-password': { status: 204 } })
    renderAt('/reset-password?userId=u1&token=abc', { demo: false })

    await user.type(await screen.findByLabelText('New password'), 'a brand new passphrase')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByRole('heading', { name: 'Password changed' })).toBeInTheDocument()
    expect(server.called('POST /api/auth/reset-password')[0]?.body).toEqual({ userId: 'u1', token: 'abc', password: 'a brand new passphrase' })
  })

  it('rejects a short password before calling the server', async () => {
    const user = userEvent.setup()
    const server = mockApi(anonymous)
    renderAt('/reset-password?userId=u1&token=abc', { demo: false })

    await user.type(await screen.findByLabelText('New password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/Use at least 10 characters/)
    expect(server.called('POST /api/auth/reset-password')).toHaveLength(0)
  })
})

describe('Protected pages', () => {
  it('sends a signed-out visitor to sign in and remembers where they were going', async () => {
    mockApi(anonymous)
    const { router } = renderAt('/monitors', { demo: false })

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(router.state.location.state).toMatchObject({ from: '/monitors' })
  })

  it('lets the demo through without an account', async () => {
    renderAt('/monitors')

    expect(await screen.findByRole('heading', { name: 'Monitors' })).toBeInTheDocument()
  })

  it('shows a real account its own monitors, never the demo ones', async () => {
    mockApi({ 'GET /api/auth/me': { body: ana }, 'GET /api/orgs/org-acme/monitors': { body: [] } })
    renderAt('/monitors', { demo: false })

    expect(await screen.findByText('No monitors yet')).toBeInTheDocument()
    expect(screen.queryByText('Public API')).not.toBeInTheDocument()
  })

  it('shows a real account its own dashboard', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/monitors': { body: [] },
      'GET /api/orgs/org-acme/summary': { body: { up: 0, down: 0, paused: 0, pending: 0, openIncidents: 0, uptime24h: null, uptime7d: null, uptime30d: null, avgResponseMs: null } },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/series?range=24h': { body: { range: '24h', series: [] } },
    })
    renderAt('/dashboard', { demo: false })

    expect(await screen.findByText('Nothing is down. Incidents appear here the moment a monitor fails its checks.')).toBeInTheDocument()
    expect(screen.getAllByText('No checks yet')).toHaveLength(3)
  })
})

describe('Invitations', () => {
  const invite = { organizationName: 'Acme Platform', email: 'bo@example.com', role: 'Admin' }

  it('sends a signed-out visitor to create an account or sign in, keeping the invite address', async () => {
    mockApi({ ...anonymous, 'GET /api/invites/tok': { body: invite } })
    renderAt('/invites/tok', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Join Acme Platform' })).toBeInTheDocument()
    const href = screen.getByRole('link', { name: 'I already have an account' }).getAttribute('href')
    expect(href).toContain('next=%2Finvites%2Ftok')
    expect(href).toContain('email=bo%40example.com')
  })

  it('joins the team for a signed-in person and opens the dashboard', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/auth/me': { body: { ...ana, email: 'bo@example.com' } },
      'GET /api/invites/tok': { body: invite },
      'POST /api/invites/tok/accept': { body: { id: 'org-acme', name: 'Acme Platform', slug: 'acme-platform', role: 'Admin' } },
    })
    const { router } = renderAt('/invites/tok', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Join Acme Platform' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
    expect(server.called('POST /api/invites/tok/accept')).toHaveLength(1)
  })

  it('explains when the signed-in account is not the invited one and offers to switch', async () => {
    const user = userEvent.setup()
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/invites/tok': { body: invite },
      'POST /api/invites/tok/accept': { status: 403, body: problem('invite_wrong_account', 'This invitation was sent to another address.') },
    })
    renderAt('/invites/tok', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Join Acme Platform' }))

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('This invitation was sent to another address.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: 'Sign out and use bo@example.com' })).toBeInTheDocument()
  })

  it('says so when the invitation is gone', async () => {
    mockApi({ ...anonymous, 'GET /api/invites/tok': { status: 404, body: problem('invite_not_found', 'This invitation has expired.') } })
    renderAt('/invites/tok', { demo: false })

    expect(await screen.findByRole('heading', { name: 'This invitation does not work' })).toBeInTheDocument()
    expect(screen.getByText('This invitation has expired.')).toBeInTheDocument()
  })
})
