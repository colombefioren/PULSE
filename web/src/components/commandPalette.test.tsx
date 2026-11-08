import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Activity } from 'lucide-react'
import { describe, expect, it } from 'vitest'
import type { ApiMonitorListItem, Me } from '@/api/types'
import { ana, mockApi } from '@/test/mockApi'
import { renderAt } from '@/test/render'
import { filterCommands, type Command } from './commands'

const command = (label: string, group: Command['group'] = 'Go to', extra: Partial<Command> = {}): Command => ({
  id: label,
  group,
  label,
  icon: Activity,
  run: () => {},
  ...extra,
})

describe('filterCommands', () => {
  const all = [
    command('Dashboard'),
    command('Create monitor', 'Actions', { keywords: 'add new' }),
    command('Docs', 'Monitors', { hint: 'https://docs.example.com' }),
    command('Auth service', 'Monitors', { hint: 'https://auth.example.com/healthz' }),
  ]

  it('returns everything for an empty search, grouped in order', () => {
    expect(filterCommands(all, '  ').map((c) => c.label)).toEqual(['Dashboard', 'Create monitor', 'Docs', 'Auth service'])
  })

  it('needs every word, in any order, and ignores case', () => {
    expect(filterCommands(all, 'SERVICE auth').map((c) => c.label)).toEqual(['Auth service'])
    expect(filterCommands(all, 'nothing like this')).toEqual([])
  })

  it('finds by hidden keywords and by address', () => {
    expect(filterCommands(all, 'add').map((c) => c.label)).toEqual(['Create monitor'])
    expect(filterCommands(all, 'healthz').map((c) => c.label)).toEqual(['Auth service'])
  })

  it('puts names that start with the search first inside their group', () => {
    const monitors = [command('Old docs', 'Monitors'), command('Docs', 'Monitors')]
    expect(filterCommands(monitors, 'docs').map((c) => c.label)).toEqual(['Docs', 'Old docs'])
  })
})

const listItem = (id: string, name: string, url: string): ApiMonitorListItem => ({
  monitor: {
    id,
    organizationId: 'org-acme',
    name,
    url,
    method: 'Get',
    intervalSeconds: 60,
    timeoutMs: 10_000,
    expectedCodes: '200-299',
    keyword: null,
    headers: {},
    failureThreshold: 3,
    recoveryThreshold: 2,
    state: 'Up',
    stateChangedAt: '2026-11-08T10:00:00Z',
    lastCheckedAt: null,
    lastResponseMs: null,
    lastStatusCode: null,
    lastErrorType: null,
    lastErrorMessage: null,
    badgeEnabled: false,
    createdAt: '2026-11-01T10:00:00Z',
    alertChannelIds: [],
  },
  uptime24h: 100,
  sparkline: [],
})

const twoTeams: Me = { ...ana, organizations: [...ana.organizations, { id: 'org-beta', name: 'Beta Labs', slug: 'beta-labs', role: 'Admin' }] }

const signedIn = (me: Me = ana) => ({
  'GET /api/auth/me': { body: me },
  'GET /api/orgs/org-acme/monitors': { body: [listItem('mon-1', 'Public API', 'https://api.example.com'), listItem('mon-2', 'Docs site', 'https://docs.example.com')] },
})

describe('Command palette', () => {
  it('opens with Ctrl+K from anywhere and closes with Escape', async () => {
    const user = userEvent.setup()
    renderAt('/alerts')
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    expect(await screen.findByRole('combobox', { name: 'Search monitors and commands' })).toHaveFocus()

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument())
  })

  it('opens with Cmd+K and from the search button', async () => {
    const user = userEvent.setup()
    renderAt('/alerts')
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Meta>}k{/Meta}')
    expect(await screen.findByRole('combobox')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument())

    await user.click(screen.getByRole('button', { name: 'Search and commands' }))
    expect(await screen.findByRole('combobox')).toBeInTheDocument()
  })

  it('jumps to a monitor found by name', async () => {
    const user = userEvent.setup()
    mockApi(signedIn())
    const { router } = renderAt('/alerts', { demo: false })
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByRole('combobox'), 'docs')
    const option = await screen.findByRole('option', { name: /Docs site/ })
    expect(screen.queryByRole('option', { name: /Public API/ })).not.toBeInTheDocument()
    expect(option).toHaveAttribute('aria-selected', 'true')
    await user.keyboard('{Enter}')

    await waitFor(() => expect(router.state.location.pathname).toBe('/monitors/mon-2'))
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })

  it('moves through results with the arrow keys', async () => {
    const user = userEvent.setup()
    mockApi(signedIn())
    const { router } = renderAt('/alerts', { demo: false })
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    const box = await screen.findByRole('combobox')
    await user.type(box, 'monitors')
    await user.keyboard('{ArrowDown}')

    const options = await screen.findAllByRole('option')
    const selected = options.find((o) => o.getAttribute('aria-selected') === 'true')
    expect(selected).toBeDefined()
    expect(box).toHaveAttribute('aria-activedescendant', selected!.id)
    await user.keyboard('{Enter}')
    await waitFor(() => expect(router.state.location.pathname).not.toBe('/alerts'))
  })

  it('goes to a page, and says so when nothing matches', async () => {
    const user = userEvent.setup()
    mockApi(signedIn())
    const { router } = renderAt('/alerts', { demo: false })
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    const box = await screen.findByRole('combobox')
    await user.type(box, 'zzzz')
    expect(await screen.findByText(/Nothing matches/)).toBeInTheDocument()

    await user.clear(box)
    await user.type(box, 'incidents')
    await user.click(await screen.findByRole('option', { name: /Incidents/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/incidents'))
  })

  it('offers to create a monitor to people who can, and not in the read-only demo', async () => {
    const user = userEvent.setup()
    mockApi(signedIn())
    const { router, unmount } = renderAt('/alerts', { demo: false })
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByRole('combobox'), 'create')
    await user.keyboard('{Enter}')
    await waitFor(() => expect(router.state.location.pathname + router.state.location.search).toBe('/monitors?new=1'))
    unmount()

    renderAt('/alerts')
    await screen.findByRole('heading', { level: 1 })
    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByRole('combobox'), 'create')
    expect(await screen.findByText(/Nothing matches/)).toBeInTheDocument()
  })

  it('switches theme', async () => {
    const user = userEvent.setup()
    renderAt('/alerts')
    await screen.findByRole('heading', { level: 1 })
    expect(document.documentElement.dataset.theme).toBe('light')

    await user.keyboard('{Control>}k{/Control}')
    await user.type(await screen.findByRole('combobox'), 'theme')
    await user.click(await screen.findByRole('option', { name: /dark theme/ }))

    await waitFor(() => expect(document.documentElement.dataset.theme).toBe('dark'))
  })

  it('switches team, listing only the other teams', async () => {
    const user = userEvent.setup()
    mockApi(signedIn(twoTeams))
    renderAt('/alerts', { demo: false })
    await screen.findByRole('heading', { level: 1 })

    await user.keyboard('{Control>}k{/Control}')
    const box = await screen.findByRole('combobox')
    await user.type(box, 'team')
    const options = await screen.findAllByRole('option')
    const team = options.filter((o) => /Switch to /.test(o.textContent ?? ''))
    expect(team.map((o) => o.textContent)).toEqual(['Switch to Beta Labs'])

    await user.click(within(screen.getByRole('listbox')).getByRole('option', { name: 'Switch to Beta Labs' }))
    await waitFor(() => expect(localStorage.getItem('pulse-org')).toBe('org-beta'))
  })
})
