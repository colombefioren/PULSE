import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderAt } from '@/test/render'

describe('Dashboard', () => {
  it('shows live counts, the open incident and the three uptime gauges', async () => {
    renderAt('/dashboard')

    expect(await screen.findByText('Monitors up')).toBeInTheDocument()
    expect(await screen.findByText('Auth service is failing health checks')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Uptime, 24 hours: .* percent/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Uptime, 7 days/ })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Uptime, 30 days/ })).toBeInTheDocument()
  })

  it('changes the response chart range', async () => {
    const user = userEvent.setup()
    renderAt('/dashboard')

    await user.click(await screen.findByRole('radio', { name: '7d' }))

    expect(screen.getByRole('radio', { name: '7d' })).toBeChecked()
    expect(await screen.findByRole('img', { name: 'All monitors response time over the last 7d' })).toBeInTheDocument()
  })

  it('acknowledges an open incident straight away', async () => {
    const user = userEvent.setup()
    renderAt('/dashboard')

    await user.click(await screen.findByRole('button', { name: 'Acknowledge' }))

    expect(await screen.findByText('Acknowledged')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
  })
})

describe('Monitors', () => {
  it('lists every monitor with its status in words', async () => {
    renderAt('/monitors')

    const table = await screen.findByRole('table')
    expect(within(table).getByRole('link', { name: 'Public API' })).toBeInTheDocument()
    expect(within(table).getAllByText('Up').length).toBeGreaterThan(3)
    expect(within(table).getByText('Down')).toBeInTheDocument()
    expect(within(table).getByText('Paused')).toBeInTheDocument()
  })

  it('filters by name and says so when nothing matches', async () => {
    const user = userEvent.setup()
    renderAt('/monitors')

    await user.type(await screen.findByRole('textbox', { name: 'Search monitors' }), 'auth')
    expect(screen.getAllByRole('link', { name: /Auth service/ })).toHaveLength(1)
    expect(screen.queryByRole('link', { name: 'Public API' })).not.toBeInTheDocument()

    await user.clear(screen.getByRole('textbox', { name: 'Search monitors' }))
    await user.type(screen.getByRole('textbox', { name: 'Search monitors' }), 'zzz')
    expect(await screen.findByRole('heading', { name: 'No monitors match "zzz"' })).toBeInTheDocument()
  })

  it('pauses and resumes a monitor', async () => {
    const user = userEvent.setup()
    renderAt('/monitors')

    await user.click(await screen.findByRole('button', { name: 'Pause Public API' }))
    expect(await screen.findByRole('button', { name: 'Resume Public API' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Resume Public API' }))
    expect(await screen.findByRole('button', { name: 'Pause Public API' })).toBeInTheDocument()
  })

  it('jumps to the search box with the slash key', async () => {
    const user = userEvent.setup()
    renderAt('/monitors')
    const search = await screen.findByRole('textbox', { name: 'Search monitors' })

    await user.keyboard('/')

    expect(search).toHaveFocus()
  })
})

describe('Monitor detail', () => {
  it('shows the monitor, its uptime figures and the response chart', async () => {
    renderAt('/monitors/mon-api')

    expect(await screen.findByRole('heading', { level: 1, name: 'Public API' })).toBeInTheDocument()
    expect(screen.getByText('Uptime, 30d')).toBeInTheDocument()
    expect(await screen.findByRole('img', { name: 'Public API response time over the last 24h' })).toBeInTheDocument()
  })

  it('lists a failing check with a specific, actionable reason', async () => {
    renderAt('/monitors/mon-auth')

    expect((await screen.findAllByText(/Timeout after 10s/)).length).toBeGreaterThan(0)
  })

  it('says when a monitor does not exist', async () => {
    renderAt('/monitors/nope')

    expect(await screen.findByRole('heading', { name: 'Monitor not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to monitors' })).toBeInTheDocument()
  })
})

describe('Incidents', () => {
  it('splits open from resolved and shows each timeline', async () => {
    renderAt('/incidents')

    expect(await screen.findByRole('heading', { name: 'Open (1)' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Resolved (3)' })).toBeInTheDocument()
    expect(screen.getByText('No failed checks for an hour. Resolved.')).toBeInTheDocument()
  })
})

describe('Public status page', () => {
  it('shows the overall banner, grouped components and the active incident', async () => {
    renderAt('/status/pulse-demo')

    expect(await screen.findByRole('heading', { name: 'Partial outage' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Core' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Web' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Public API: 90 days of uptime/ })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Auth service is failing health checks' })).toBeInTheDocument()
  })

  it('does not need an account', async () => {
    renderAt('/status/pulse-demo')

    await screen.findByRole('heading', { name: 'Partial outage' })
    expect(screen.queryByRole('navigation', { name: 'Main' })).not.toBeInTheDocument()
  })

  it('validates the subscribe form inline and confirms by email', async () => {
    const user = userEvent.setup()
    renderAt('/status/pulse-demo')

    const input = await screen.findByLabelText('Email address')
    await user.click(screen.getByRole('button', { name: /Subscribe to updates/ }))
    expect(await screen.findByText('Enter an email address like name@company.com')).toBeInTheDocument()

    await user.type(input, 'ana@acme.dev')
    await user.click(screen.getByRole('button', { name: /Subscribe to updates/ }))
    expect(await screen.findByRole('status')).toHaveTextContent('Check ana@acme.dev to confirm')
  })

  it('says so plainly for an unknown slug', async () => {
    renderAt('/status/nothing-here')

    expect(await screen.findByRole('heading', { name: 'No status page here' })).toBeInTheDocument()
  })
})

describe('Landing', () => {
  it('leads with the value statement and the two ways in', async () => {
    renderAt('/')

    expect(await screen.findByRole('heading', { level: 1, name: /Know when it breaks/ })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /Try the demo/ }).length).toBeGreaterThan(0)
    expect(screen.getByRole('link', { name: 'Sign up' })).toHaveAttribute('href', '/signup')
  })

  it('embeds the live status page', async () => {
    renderAt('/')

    expect(await screen.findByRole('heading', { name: 'Partial outage' })).toBeInTheDocument()
  })
})

describe('Sign in', () => {
  it('shows an error under the email once it loses focus with a bad value', async () => {
    const user = userEvent.setup()
    renderAt('/login')

    await user.type(await screen.findByLabelText('Email'), 'colombe')
    await user.tab()

    expect(await screen.findByText('Enter an email like name@company.com')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
  })

  it('asks for the password and does not continue with an empty form', async () => {
    const user = userEvent.setup()
    renderAt('/login')

    await user.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter your password')).toBeInTheDocument()
  })

  it('still offers the demo for people who are just looking', async () => {
    renderAt('/login')

    expect(await screen.findByRole('link', { name: 'Try the demo' })).toHaveAttribute('href', '/demo')
  })
})

describe('Sign up', () => {
  it('previews the status page address from the team name', async () => {
    const user = userEvent.setup()
    renderAt('/signup')

    await user.type(await screen.findByLabelText('Team name'), 'Acme Platform!')

    expect(screen.getByText('/status/acme-platform')).toBeInTheDocument()
  })

  it('says how many more characters the password needs and rates it as it grows', async () => {
    const user = userEvent.setup()
    renderAt('/signup')

    const password = await screen.findByLabelText('Password')
    await user.type(password, 'short')
    await user.tab()
    expect(await screen.findByText('Use at least 10 characters (5 more)')).toBeInTheDocument()

    await user.clear(password)
    await user.type(password, 'Correct-Horse-Battery-9')
    expect(screen.getByText('Strong')).toBeInTheDocument()
  })

  it('shows every missing field at once when submitted empty', async () => {
    const user = userEvent.setup()
    renderAt('/signup')

    await user.click(await screen.findByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(screen.getAllByRole('alert').length).toBe(4))
  })
})
