import { QueryClient } from '@tanstack/react-query'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { ApiMonitor, ApiMonitorListItem, ApiMonitorStats, Me } from '@/api/types'
import { parseHeaders } from '@/data/monitorView'
import { monitorKeys } from '@/data/monitors'
import type { MonitorView } from '@/data/monitorView'
import { applyCheckRecorded, applyMonitorChanged } from '@/live/apply'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const monitor = (over: Partial<ApiMonitor> = {}): ApiMonitor => ({
  id: 'mon-1',
  organizationId: 'org-acme',
  name: 'Public API',
  url: 'https://api.example.com/health',
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
  lastCheckedAt: new Date().toISOString(),
  lastResponseMs: 142,
  lastStatusCode: 200,
  lastErrorType: null,
  lastErrorMessage: null,
  badgeEnabled: false,
  createdAt: '2026-11-01T10:00:00Z',
  alertChannelIds: [],
  ...over,
})

const item = (over: Partial<ApiMonitor> = {}, uptime24h: number | null = 99.5): ApiMonitorListItem => ({
  monitor: monitor(over),
  uptime24h,
  sparkline: Array.from({ length: 24 }, (_, i) => (i % 5 === 0 ? null : 100 + i)),
})

const stats: ApiMonitorStats = {
  range: '24h',
  checks: 1440,
  uptimePercent: 99.9,
  avgMs: 150,
  p50Ms: 140,
  p95Ms: 320,
  p99Ms: 810,
  uptime: { last24Hours: 99.9, last7Days: 99.5, last30Days: 99.1, last90Days: 98.7 },
  series: [
    { at: '2026-11-08T09:00:00Z', avgMs: 140, checks: 60, failures: 0 },
    { at: '2026-11-08T10:00:00Z', avgMs: 160, checks: 60, failures: 1 },
  ],
}

const as = (role: 'Owner' | 'Admin' | 'Viewer'): Me => ({ ...ana, organizations: [{ ...ana.organizations[0]!, role }] })

const signedIn = (role: 'Owner' | 'Admin' | 'Viewer' = 'Owner', monitors: ApiMonitorListItem[] = [item()]) => ({
  'GET /api/auth/me': { body: as(role) },
  'GET /api/orgs/org-acme/monitors': { body: monitors },
})

const detail = (m: ApiMonitor = monitor()) => ({
  [`GET /api/monitors/${m.id}`]: { body: m },
  [`GET /api/monitors/${m.id}/stats?range=24h`]: { body: stats },
  [`GET /api/monitors/${m.id}/checks?limit=25`]: {
    body: {
      items: [
        { checkedAt: '2026-11-08T10:01:00Z', success: false, statusCode: 503, responseMs: 220, errorType: 'UnexpectedStatus', errorMessage: 'Expected 200–299, got 503' },
        { checkedAt: '2026-11-08T10:00:00Z', success: true, statusCode: 200, responseMs: 140, errorType: null, errorMessage: null },
      ],
      nextBefore: '2026-01-01T00:00:00.000Z',
    },
  },
})

describe('Monitors list (signed in)', () => {
  it('shows the team’s monitors with their status, uptime and response time', async () => {
    mockApi(signedIn('Owner', [item(), item({ id: 'mon-2', name: 'Docs', url: 'https://docs.example.com', state: 'Down' }, null)]))
    renderAt('/monitors', { demo: false })

    const link = await screen.findByRole('link', { name: 'Public API' })
    const row = link.closest('tr')!
    expect(within(row).getByText('Up')).toBeInTheDocument()
    expect(within(row).getByText('99.5%')).toBeInTheDocument()
    expect(within(row).getByText('142 ms')).toBeInTheDocument()
    const docs = screen.getByRole('link', { name: 'Docs' }).closest('tr')!
    expect(within(docs).getByText('Down')).toBeInTheDocument()
  })

  it('creates a monitor with the entered values', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...signedIn('Owner', []),
      'POST /api/orgs/org-acme/monitors': { status: 201, body: monitor({ id: 'mon-new', name: 'Marketing', url: 'https://example.com', state: 'Pending' }) },
    })
    renderAt('/monitors', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Add monitor' }))
    await user.type(screen.getByLabelText('Name'), 'Marketing')
    await user.type(screen.getByLabelText('URL'), 'https://example.com')
    await user.selectOptions(screen.getByLabelText('Check'), '300')
    await user.click(screen.getByRole('button', { name: 'Add monitor', hidden: false, description: '' }))

    await waitFor(() => expect(server.called('POST /api/orgs/org-acme/monitors')).toHaveLength(1))
    expect(server.called('POST /api/orgs/org-acme/monitors')[0]?.body).toMatchObject({
      name: 'Marketing',
      url: 'https://example.com',
      method: 'Get',
      intervalSeconds: 300,
      timeoutMs: 10_000,
      expectedCodes: null,
      headers: {},
    })
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('opens the form from the C shortcut address and shows server reasons next to the field', async () => {
    const user = userEvent.setup()
    mockApi({
      ...signedIn('Admin', []),
      'POST /api/orgs/org-acme/monitors': {
        status: 400,
        body: problem('validation_failed', 'Some fields need attention.', { url: ['That address points to a private network, so it can’t be checked'] }),
      },
    })
    renderAt('/monitors?new=1', { demo: false })

    await user.type(await screen.findByLabelText('Name'), 'Internal')
    await user.type(screen.getByLabelText('URL'), 'https://10.0.0.5')
    await user.click(screen.getByRole('button', { name: 'Add monitor', description: '' }))

    expect(await screen.findByText(/points to a private network/)).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('checks the form before sending anything', async () => {
    const user = userEvent.setup()
    const server = mockApi(signedIn('Owner', []))
    renderAt('/monitors?new=1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Add monitor', description: '' }))

    expect(await screen.findByText('Enter a name')).toBeInTheDocument()
    expect(screen.getByText(/Enter the address to check/)).toBeInTheDocument()
    expect(server.called('POST /api/orgs/org-acme/monitors')).toHaveLength(0)
  })

  it('pauses a monitor straight away', async () => {
    const user = userEvent.setup()
    let paused = false
    const server = mockApi({
      ...signedIn(),
      'GET /api/orgs/org-acme/monitors': () => ({ body: [item(paused ? { state: 'Paused' } : {})] }),
      'POST /api/monitors/mon-1/pause': () => {
        paused = true
        return { body: monitor({ state: 'Paused' }) }
      },
    })
    renderAt('/monitors', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Pause Public API' }))

    expect(await screen.findByText('Paused')).toBeInTheDocument()
    expect(server.called('POST /api/monitors/mon-1/pause')).toHaveLength(1)
  })

  it('puts the row back when pausing fails', async () => {
    const user = userEvent.setup()
    mockApi({ ...signedIn(), 'POST /api/monitors/mon-1/pause': { status: 500, body: problem('server_error', 'Something went wrong.') } })
    renderAt('/monitors', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Pause Public API' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Pause Public API' })).toBeInTheDocument())
    expect(screen.queryByText('Paused')).not.toBeInTheDocument()
  })

  it('is read-only for a Viewer', async () => {
    mockApi(signedIn('Viewer'))
    renderAt('/monitors', { demo: false })

    expect(await screen.findByRole('link', { name: 'Public API' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add monitor' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Pause/ })).not.toBeInTheDocument()
  })

  it('tells a Viewer who can add the first monitor', async () => {
    mockApi(signedIn('Viewer', []))
    renderAt('/monitors', { demo: false })

    expect(await screen.findByText('No monitors yet')).toBeInTheDocument()
    expect(screen.getByText(/Owner or Admin/)).toBeInTheDocument()
  })

  it('offers no add button in the demo', async () => {
    renderAt('/monitors')

    expect(await screen.findByRole('heading', { name: 'Monitors' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add monitor' })).not.toBeInTheDocument()
  })
})

describe('Monitor detail (signed in)', () => {
  it('shows real figures, and a failed check says why', async () => {
    mockApi({ ...signedIn(), ...detail() })
    renderAt('/monitors/mon-1', { demo: false })

    expect(await screen.findByRole('heading', { level: 1, name: 'Public API' })).toBeInTheDocument()
    expect(await screen.findByText('99.1%')).toBeInTheDocument()
    expect(screen.getByText('810 ms')).toBeInTheDocument()
    expect(await screen.findByText('Expected 200–299, got 503')).toBeInTheDocument()
    expect(screen.getByText('200 OK')).toBeInTheDocument()
  })

  it('shows the 90-day uptime next to the shorter ones', async () => {
    mockApi({ ...signedIn(), ...detail() })
    renderAt('/monitors/mon-1', { demo: false })

    expect(await screen.findByText('Uptime, 90d')).toBeInTheDocument()
    expect(await screen.findByText('98.7%')).toBeInTheDocument()
  })

  it('shows the badge address to copy only once the badge is on', async () => {
    mockApi({ ...signedIn(), ...detail(monitor({ badgeEnabled: true })) })
    renderAt('/monitors/mon-1', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Uptime badge' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Public API uptime badge' })).toHaveAttribute('src', '/badge/mon-1.svg')
    expect(screen.getByTestId('badge-markdown')).toHaveTextContent('![Public API uptime](http://localhost:3000/badge/mon-1.svg)'.replace('http://localhost:3000', window.location.origin))
    expect(screen.getByRole('button', { name: 'Copy Markdown' })).toBeInTheDocument()
  })

  it('has no badge panel while the badge is off', async () => {
    mockApi({ ...signedIn(), ...detail() })
    renderAt('/monitors/mon-1', { demo: false })

    await screen.findByRole('heading', { level: 1, name: 'Public API' })
    expect(screen.queryByRole('heading', { name: 'Uptime badge' })).not.toBeInTheDocument()
  })

  it('switches the badge on from the edit form', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...signedIn(),
      ...detail(),
      'PATCH /api/monitors/mon-1': { body: monitor({ badgeEnabled: true }) },
    })
    renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Edit' }))
    const toggle = screen.getByRole('checkbox', { name: 'Public uptime badge' })
    expect(toggle).not.toBeChecked()
    await user.click(screen.getByText('Advanced'))
    await user.click(toggle)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(server.called('PATCH /api/monitors/mon-1')).toHaveLength(1))
    expect(server.called('PATCH /api/monitors/mon-1')[0]?.body).toMatchObject({ badgeEnabled: true })
  })

  it('loads older checks on request', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...signedIn(),
      ...detail(),
      'GET /api/monitors/mon-1/checks?limit=25&before=2026-01-01T00%3A00%3A00.000Z': {
        body: { items: [{ checkedAt: '2025-12-31T23:00:00Z', success: false, statusCode: null, responseMs: 1000, errorType: 'Timeout', errorMessage: 'Timeout after 1s — the server didn’t respond' }], nextBefore: null },
      },
    })
    renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Load more' }))

    expect(await screen.findByText(/Timeout after 1s/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
    expect(server.called('GET /api/monitors/mon-1/checks?limit=25&before=2026-01-01T00%3A00%3A00.000Z')).toHaveLength(1)
  })

  it('edits a monitor with its current values filled in', async () => {
    const user = userEvent.setup()
    let saved = monitor()
    const server = mockApi({
      ...signedIn(),
      ...detail(),
      'GET /api/monitors/mon-1': () => ({ body: saved }),
      'PATCH /api/monitors/mon-1': () => {
        saved = monitor({ name: 'Public API v2' })
        return { body: saved }
      },
    })
    renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Edit' }))
    const name = screen.getByLabelText('Name')
    expect(name).toHaveValue('Public API')
    expect(screen.getByLabelText('URL')).toHaveValue('https://api.example.com/health')
    await user.clear(name)
    await user.type(name, 'Public API v2')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(server.called('PATCH /api/monitors/mon-1')).toHaveLength(1))
    expect(server.called('PATCH /api/monitors/mon-1')[0]?.body).toMatchObject({ name: 'Public API v2', url: 'https://api.example.com/health' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Public API v2' })).toBeInTheDocument()
  })

  it('shows a conflict from a simultaneous edit instead of failing silently', async () => {
    const user = userEvent.setup()
    mockApi({
      ...signedIn(),
      ...detail(),
      'PATCH /api/monitors/mon-1': { status: 409, body: problem('conflict', 'Someone else changed this monitor. Reload and try again.') },
    })
    renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Edit' }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText(/Someone else changed this monitor/)).toBeInTheDocument()
  })

  it('asks before deleting, then returns to the list', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...signedIn(), ...detail(), 'DELETE /api/monitors/mon-1': { status: 204 } })
    const { router } = renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delete' }))
    expect(server.called('DELETE /api/monitors/mon-1')).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Delete monitor' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/monitors'))
    expect(server.called('DELETE /api/monitors/mon-1')).toHaveLength(1)
  })

  it('hides edit and delete from a Viewer', async () => {
    mockApi({ ...signedIn('Viewer'), ...detail() })
    renderAt('/monitors/mon-1', { demo: false })

    expect(await screen.findByRole('heading', { level: 1, name: 'Public API' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Pause' })).not.toBeInTheDocument()
  })

  it('says a monitor of another team is not found', async () => {
    mockApi({ ...signedIn(), 'GET /api/monitors/other': { status: 404, body: problem('monitor_not_found', 'Monitor not found.') } })
    renderAt('/monitors/other', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Monitor not found' })).toBeInTheDocument()
  })
})

describe('parseHeaders', () => {
  it('reads one header per line', () => {
    expect(parseHeaders('Authorization: Bearer abc\n\nX-Env:  prod ')).toEqual({
      headers: { Authorization: 'Bearer abc', 'X-Env': 'prod' },
      error: null,
    })
  })

  it('keeps colons inside the value', () => {
    expect(parseHeaders('X-Url: https://a.example/b').headers).toEqual({ 'X-Url': 'https://a.example/b' })
  })

  it('names the line it cannot read', () => {
    expect(parseHeaders('nonsense').error).toContain('nonsense')
    expect(parseHeaders(': value').error).not.toBeNull()
  })
})

describe('live updates', () => {
  const view = (over: Partial<MonitorView> = {}): MonitorView => ({
    id: 'mon-1',
    name: 'Public API',
    url: 'https://api.example.com',
    method: 'GET',
    intervalS: 60,
    timeoutMs: 10_000,
    expectedCodes: '200-299',
    keyword: null,
    headers: {},
    failureThreshold: 3,
    recoveryThreshold: 2,
    alertChannelIds: [],
    badgeEnabled: false,
    status: 'up',
    lastResponseMs: 100,
    lastCheckedAt: Date.parse('2026-11-08T10:00:00Z'),
    lastError: null,
    uptime24h: 99,
    spark: [],
    ...over,
  })

  const event = (over = {}) => ({
    monitorId: 'mon-1',
    checkedAt: '2026-11-08T10:01:00Z',
    success: false,
    statusCode: 503,
    responseMs: 250,
    errorType: 'UnexpectedStatus',
    errorMessage: 'Expected 200–299, got 503',
    state: 'Down' as const,
    ...over,
  })

  it('patches the list and the detail with a new result', () => {
    const client = new QueryClient()
    client.setQueryData(monitorKeys.list('org-acme'), [view(), view({ id: 'mon-2' })])
    client.setQueryData(monitorKeys.one('mon-1'), view())

    applyCheckRecorded(client, 'org-acme', event())

    const list = client.getQueryData<MonitorView[]>(monitorKeys.list('org-acme'))!
    expect(list[0]).toMatchObject({ status: 'down', lastResponseMs: 250, lastError: 'Expected 200–299, got 503' })
    expect(list[1]?.status).toBe('up')
    expect(client.getQueryData<MonitorView>(monitorKeys.one('mon-1'))?.status).toBe('down')
  })

  it('ignores a result older than the one on screen', () => {
    const client = new QueryClient()
    client.setQueryData(monitorKeys.list('org-acme'), [view()])

    applyCheckRecorded(client, 'org-acme', event({ checkedAt: '2026-11-08T09:00:00Z' }))

    expect(client.getQueryData<MonitorView[]>(monitorKeys.list('org-acme'))?.[0]?.status).toBe('up')
  })

  it('puts a new check at the top of the loaded checks, once', () => {
    const client = new QueryClient()
    client.setQueryData(monitorKeys.checks('mon-1'), {
      pages: [{ items: [{ at: Date.parse('2026-11-08T10:00:00Z'), success: true, statusCode: 200, responseMs: 100, message: null }], next: null }],
      pageParams: [null],
    })

    applyCheckRecorded(client, 'org-acme', event())
    applyCheckRecorded(client, 'org-acme', event())

    const data = client.getQueryData<{ pages: { items: { at: number }[] }[] }>(monitorKeys.checks('mon-1'))!
    expect(data.pages[0]?.items).toHaveLength(2)
    expect(data.pages[0]?.items[0]?.at).toBe(Date.parse('2026-11-08T10:01:00Z'))
  })

  it('drops a deleted monitor and refreshes the list for other changes', () => {
    const client = new QueryClient()
    client.setQueryData(monitorKeys.one('mon-1'), view())

    applyMonitorChanged(client, 'org-acme', { monitorId: 'mon-1', kind: 'deleted' })

    expect(client.getQueryData(monitorKeys.one('mon-1'))).toBeUndefined()
  })
})
