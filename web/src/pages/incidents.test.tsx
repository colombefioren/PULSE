import { QueryClient } from '@tanstack/react-query'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type {
  ApiAlertChannel,
  ApiDelivery,
  ApiIncident,
  ApiIncidentDetail,
  ApiMonitor,
  ApiMonitorListItem,
  Me,
} from '@/api/types'
import { incidentKeys } from '@/data/incidents'
import { applyIncidentChanged } from '@/live/apply'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const as = (role: 'Owner' | 'Admin' | 'Viewer'): Me => ({ ...ana, organizations: [{ ...ana.organizations[0]!, role }] })

const incident = (over: Partial<ApiIncident> = {}): ApiIncident => ({
  id: 'inc-1',
  organizationId: 'org-acme',
  monitorId: 'mon-1',
  monitorName: 'Public API',
  title: 'Public API is down',
  severity: 'MajorOutage',
  status: 'Investigating',
  startedAt: new Date(Date.now() - 600_000).toISOString(),
  acknowledgedAt: null,
  acknowledgedBy: null,
  resolvedAt: null,
  isManual: false,
  ...over,
})

const delivery = (over: Partial<ApiDelivery> = {}): ApiDelivery => ({
  id: 'del-1',
  channelId: 'ch-1',
  channelName: 'On-call',
  channelType: 'Email',
  incidentId: 'inc-1',
  kind: 'IncidentOpened',
  status: 'Sent',
  attempts: 1,
  createdAt: new Date(Date.now() - 590_000).toISOString(),
  sentAt: new Date(Date.now() - 590_000).toISOString(),
  error: null,
  ...over,
})

const detail = (over: Partial<ApiIncident> = {}, alerts: ApiDelivery[] = [delivery()]): ApiIncidentDetail => ({
  incident: incident(over),
  updates: [
    { id: 'u-1', kind: 'Opened', status: 'Investigating', message: '3 failed checks in a row. Last error: received 503', author: null, createdAt: '2026-11-08T10:00:00Z' },
  ],
  alerts,
  components: [],
})

const channel = (over: Partial<ApiAlertChannel> = {}): ApiAlertChannel => ({
  id: 'ch-1',
  organizationId: 'org-acme',
  type: 'Email',
  name: 'On-call',
  recipients: ['oncall@acme.dev'],
  destination: null,
  monitorCount: 2,
  lastDelivery: null,
  createdAt: '2026-11-01T10:00:00Z',
  ...over,
})

const openPage = (items: ApiIncident[]) => ({ body: { items, nextBefore: null } })

describe('Incidents for a real account', () => {
  it('lists open and resolved incidents from the API', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': openPage([incident()]),
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': openPage([
        incident({ id: 'inc-0', title: 'Checkout was slow', status: 'Resolved', severity: 'Degraded', resolvedAt: new Date().toISOString() }),
      ]),
    })
    renderAt('/incidents', { demo: false })

    expect(await screen.findByRole('link', { name: 'Public API is down' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Checkout was slow' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Open (1)' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Resolved (1)' })).toBeInTheDocument()
  })

  it('says what happens when there are none, and offers to declare one', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': openPage([]),
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': openPage([]),
    })
    renderAt('/incidents', { demo: false })

    expect(await screen.findByRole('heading', { name: 'No incidents' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Declare incident/ }).length).toBeGreaterThan(0)
  })

  it('acknowledges an incident', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': openPage([incident()]),
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': openPage([]),
      'POST /api/incidents/inc-1/acknowledge': { body: detail() },
    })
    renderAt('/incidents', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Acknowledge' }))

    await waitFor(() => expect(server.called('POST /api/incidents/inc-1/acknowledge')).toHaveLength(1))
    expect(server.called('POST /api/incidents/inc-1/acknowledge')[0]?.csrf).toBe('test-token')
  })

  it('hides the write controls from a viewer', async () => {
    mockApi({
      'GET /api/auth/me': { body: as('Viewer') },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': openPage([incident()]),
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': openPage([]),
    })
    renderAt('/incidents', { demo: false })

    await screen.findByRole('link', { name: 'Public API is down' })
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Declare incident/ })).not.toBeInTheDocument()
  })

  it('declares an incident by hand and opens it', async () => {
    const user = userEvent.setup()
    const created = detail({ id: 'inc-9', title: 'Dashboard is slow', isManual: true, monitorId: null, monitorName: null, severity: 'Degraded' })
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': openPage([]),
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': openPage([]),
      'GET /api/orgs/org-acme/monitors': { body: [] },
      'POST /api/orgs/org-acme/incidents': { status: 201, body: created },
      'GET /api/incidents/inc-9': { body: created },
    })
    const { router } = renderAt('/incidents', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Declare incident/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Declare incident' }))
    expect(await within(dialog).findByText('Enter a title')).toBeInTheDocument()
    expect(within(dialog).getByText('Say what is happening')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Title'), 'Dashboard is slow')
    await user.type(within(dialog).getByLabelText('What is happening'), 'Looking into it.')
    await user.click(within(dialog).getByRole('button', { name: 'Declare incident' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/incidents/inc-9'))
    expect(server.called('POST /api/orgs/org-acme/incidents')[0]?.body).toMatchObject({
      title: 'Dashboard is slow',
      severity: 'Degraded',
      status: 'Investigating',
      message: 'Looking into it.',
    })
  })
})

describe('Incident detail', () => {
  const base = { 'GET /api/auth/me': { body: ana } }

  it('shows the timeline and the alerts that were sent', async () => {
    mockApi({
      ...base,
      'GET /api/incidents/inc-1': { body: detail({}, [delivery(), delivery({ id: 'del-2', channelName: 'Ops', channelType: 'Slack', status: 'Failed', attempts: 5 })]) },
    })
    renderAt('/incidents/inc-1', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Public API is down' })).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Timeline' })).toHaveTextContent('received 503')
    const alerts = screen.getByRole('heading', { name: 'Alerts sent' }).closest('section')!
    expect(within(alerts).getByText('On-call')).toBeInTheDocument()
    expect(within(alerts).getByText('Failed')).toBeInTheDocument()
  })

  it('says so for an incident that is not there', async () => {
    mockApi({ ...base, 'GET /api/incidents/nope': { status: 404, body: problem('incident_not_found', 'That incident does not exist.') } })
    renderAt('/incidents/nope', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Incident not found' })).toBeInTheDocument()
  })

  it('adds an internal note to a monitor incident', async () => {
    const user = userEvent.setup()
    const noted = detail()
    noted.updates.push({ id: 'u-2', kind: 'Note', status: 'Investigating', message: 'Restarting the pod.', author: 'Ana Rakoto', createdAt: '2026-11-08T10:05:00Z' })
    const server = mockApi({
      ...base,
      'GET /api/incidents/inc-1': { body: detail() },
      'POST /api/incidents/inc-1/updates': { body: noted },
    })
    renderAt('/incidents/inc-1', { demo: false })

    await user.type(await screen.findByLabelText('Add a note'), 'Restarting the pod.')
    await user.click(screen.getByRole('button', { name: 'Add note' }))

    expect(await screen.findByText('Restarting the pod.')).toBeInTheDocument()
    expect(server.called('POST /api/incidents/inc-1/updates')[0]?.body).toEqual({ message: 'Restarting the pod.', status: null })
  })

  it('posts a public status update on a manual incident', async () => {
    const user = userEvent.setup()
    const manual = detail({ isManual: true, monitorId: null, monitorName: null })
    const server = mockApi({
      ...base,
      'GET /api/incidents/inc-1': { body: manual },
      'POST /api/incidents/inc-1/updates': { body: manual },
    })
    renderAt('/incidents/inc-1', { demo: false })

    await user.selectOptions(await screen.findByLabelText('Post as'), 'Identified')
    await user.type(screen.getByLabelText('Message'), 'We found the cause.')
    await user.click(screen.getByRole('button', { name: 'Post update' }))

    await waitFor(() => expect(server.called('POST /api/incidents/inc-1/updates')).toHaveLength(1))
    expect(server.called('POST /api/incidents/inc-1/updates')[0]?.body).toEqual({ message: 'We found the cause.', status: 'Identified' })
  })

  it('asks for a message before posting', async () => {
    const user = userEvent.setup()
    mockApi({ ...base, 'GET /api/incidents/inc-1': { body: detail() } })
    renderAt('/incidents/inc-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Add note' }))

    expect(await screen.findByText('Write a message')).toBeInTheDocument()
  })

  it('offers no way to write to a resolved incident', async () => {
    mockApi({ ...base, 'GET /api/incidents/inc-1': { body: detail({ status: 'Resolved', resolvedAt: new Date().toISOString() }) } })
    renderAt('/incidents/inc-1', { demo: false })

    await screen.findByRole('heading', { name: 'Public API is down' })
    expect(screen.queryByRole('button', { name: 'Add note' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).not.toBeInTheDocument()
  })
})

describe('Alert channels', () => {
  const base = { 'GET /api/auth/me': { body: ana } }

  it('lists channels without ever showing a webhook address', async () => {
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': {
        body: [channel(), channel({ id: 'ch-2', type: 'Slack', name: '#ops', recipients: [], destination: 'hooks.slack.com', monitorCount: 0 })],
      },
    })
    renderAt('/alerts', { demo: false })

    expect(await screen.findByText('oncall@acme.dev')).toBeInTheDocument()
    expect(screen.getByText('hooks.slack.com')).toBeInTheDocument()
    expect(screen.getByText('Not used by any monitor yet')).toBeInTheDocument()
    expect(screen.getByText('Used by 2 monitors')).toBeInTheDocument()
  })

  it('shows an empty state with a way to add the first channel', async () => {
    mockApi({ ...base, 'GET /api/orgs/org-acme/alert-channels': { body: [] } })
    renderAt('/alerts', { demo: false })

    expect(await screen.findByRole('heading', { name: 'No alert channels yet' })).toBeInTheDocument()
  })

  it('adds an email channel', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [] },
      'POST /api/orgs/org-acme/alert-channels': { status: 201, body: { channel: channel(), signingSecret: null } },
    })
    renderAt('/alerts', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: 'Add channel' }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Add channel' }))
    expect(await within(dialog).findByText('Enter a name')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter at least one email address')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Name'), 'On-call')
    await user.type(within(dialog).getByLabelText('Send to'), 'oncall@acme.dev{enter}lead@acme.dev')
    await user.click(within(dialog).getByRole('button', { name: 'Add channel' }))

    await waitFor(() => expect(server.called('POST /api/orgs/org-acme/alert-channels')).toHaveLength(1))
    expect(server.called('POST /api/orgs/org-acme/alert-channels')[0]?.body).toEqual({
      type: 'Email',
      name: 'On-call',
      recipients: ['oncall@acme.dev', 'lead@acme.dev'],
    })
  })

  it('shows the signing secret of a new webhook once', async () => {
    const user = userEvent.setup()
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [] },
      'POST /api/orgs/org-acme/alert-channels': {
        status: 201,
        body: { channel: channel({ id: 'ch-3', type: 'Webhook', name: 'Mine', recipients: [], destination: 'example.com' }), signingSecret: 'whsec_abc123' },
      },
    })
    renderAt('/alerts', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: 'Add channel' }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.selectOptions(within(dialog).getByLabelText('Type'), 'Webhook')
    await user.type(within(dialog).getByLabelText('Name'), 'Mine')
    await user.type(within(dialog).getByLabelText('Webhook address'), 'https://example.com/hook')
    await user.click(within(dialog).getByRole('button', { name: 'Add channel' }))

    expect(await screen.findByTestId('signing-secret')).toHaveTextContent('whsec_abc123')
    await user.click(screen.getByRole('button', { name: 'I have saved it' }))
    expect(screen.queryByTestId('signing-secret')).not.toBeInTheDocument()
  })

  it('puts the server\'s message next to the field it is about', async () => {
    const user = userEvent.setup()
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [] },
      'POST /api/orgs/org-acme/alert-channels': {
        status: 400,
        body: problem('validation_failed', 'Check the form.', { url: ['Use the webhook address from Slack, like https://hooks.slack.com/services/...'] }),
      },
    })
    renderAt('/alerts', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: 'Add channel' }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.selectOptions(within(dialog).getByLabelText('Type'), 'Slack')
    await user.type(within(dialog).getByLabelText('Name'), 'Ops')
    await user.type(within(dialog).getByLabelText('Webhook address'), 'https://example.com/x')
    await user.click(within(dialog).getByRole('button', { name: 'Add channel' }))

    expect(await within(dialog).findByText(/Use the webhook address from Slack/)).toBeInTheDocument()
  })

  it('sends a test alert and reports it', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [channel()] },
      'POST /api/alert-channels/ch-1/test': { body: delivery({ kind: 'Test', incidentId: null }) },
    })
    renderAt('/alerts', { demo: false })

    await user.click(await screen.findByRole('button', { name: /Send test alert/ }))

    expect(await screen.findByText('Test alert sent.')).toBeInTheDocument()
    expect(server.called('POST /api/alert-channels/ch-1/test')).toHaveLength(1)
  })

  it('says why a test alert failed', async () => {
    const user = userEvent.setup()
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [channel({ type: 'Webhook', recipients: [], destination: 'example.com' })] },
      'POST /api/alert-channels/ch-1/test': { body: delivery({ kind: 'Test', incidentId: null, status: 'Failed', error: 'The address answered 500.' }) },
    })
    renderAt('/alerts', { demo: false })

    await user.click(await screen.findByRole('button', { name: /Send test alert/ }))

    expect(await screen.findByText(/did not go through: The address answered 500./)).toBeInTheDocument()
  })

  it('shows the wait when tests are sent too fast', async () => {
    const user = userEvent.setup()
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [channel()] },
      'POST /api/alert-channels/ch-1/test': { status: 429, body: problem('too_many_tests', 'Wait a minute before sending more test alerts.') },
    })
    renderAt('/alerts', { demo: false })

    await user.click(await screen.findByRole('button', { name: /Send test alert/ }))

    expect(await screen.findByText('Wait a minute before sending more test alerts.')).toBeInTheDocument()
  })

  it('shows delivery history on request', async () => {
    const user = userEvent.setup()
    mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [channel()] },
      'GET /api/alert-channels/ch-1/deliveries?limit=15': {
        body: { items: [delivery({ status: 'Failed', attempts: 5, error: 'The address answered 500.' })], nextBefore: null },
      },
    })
    renderAt('/alerts', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delivery history' }))

    const list = await screen.findByRole('list', { name: 'Recent alerts' })
    expect(list).toHaveTextContent('Went down')
    expect(list).toHaveTextContent('Failed')
    expect(list).toHaveTextContent('The address answered 500.')
  })

  it('asks before deleting a channel', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base,
      'GET /api/orgs/org-acme/alert-channels': { body: [channel()] },
      'DELETE /api/alert-channels/ch-1': { status: 204 },
    })
    renderAt('/alerts', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delete On-call' }))
    const dialog = await screen.findByRole('dialog')
    expect(server.called('DELETE /api/alert-channels/ch-1')).toHaveLength(0)
    await user.click(within(dialog).getByRole('button', { name: 'Delete channel' }))

    await waitFor(() => expect(server.called('DELETE /api/alert-channels/ch-1')).toHaveLength(1))
  })

  it('lets a viewer read channels but not change them', async () => {
    mockApi({ 'GET /api/auth/me': { body: as('Viewer') }, 'GET /api/orgs/org-acme/alert-channels': { body: [channel()] } })
    renderAt('/alerts', { demo: false })

    await screen.findByText('oncall@acme.dev')
    expect(screen.queryByRole('button', { name: /Send test alert/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add channel' })).not.toBeInTheDocument()
  })

  it('shows example channels in the demo', async () => {
    renderAt('/alerts')

    expect(await screen.findByText('#ops-alerts')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add channel' })).not.toBeInTheDocument()
  })
})

describe('Alert channels on a monitor', () => {
  const apiMonitor = (over: Partial<ApiMonitor> = {}): ApiMonitor => ({
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
    lastResponseMs: 100,
    lastStatusCode: 200,
    lastErrorType: null,
    lastErrorMessage: null,
    badgeEnabled: false,
    createdAt: '2026-11-01T10:00:00Z',
    alertChannelIds: [],
    ...over,
  })
  const listItem = (monitor: ApiMonitor): ApiMonitorListItem => ({ monitor, uptime24h: 100, sparkline: [] })

  it('alerts every channel by default for a new monitor and sends the choice', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/monitors': { body: [] },
      'GET /api/orgs/org-acme/alert-channels': { body: [channel(), channel({ id: 'ch-2', name: 'Ops', type: 'Slack', recipients: [], destination: 'hooks.slack.com' })] },
      'POST /api/orgs/org-acme/monitors': { status: 201, body: apiMonitor() },
    })
    renderAt('/monitors?new=1', { demo: false })

    const dialog = await screen.findByRole('dialog')
    const onCall = await within(dialog).findByRole('checkbox', { name: /On-call/ })
    const ops = within(dialog).getByRole('checkbox', { name: /Ops/ })
    expect(onCall).toBeChecked()
    expect(ops).toBeChecked()

    await user.click(ops)
    await user.type(within(dialog).getByLabelText('Name'), 'Public API')
    await user.type(within(dialog).getByLabelText('URL'), 'https://api.example.com/health')
    await user.click(within(dialog).getByRole('button', { name: 'Add monitor' }))

    await waitFor(() => expect(server.called('POST /api/orgs/org-acme/monitors')).toHaveLength(1))
    expect(server.called('POST /api/orgs/org-acme/monitors')[0]?.body).toMatchObject({ alertChannelIds: ['ch-1'] })
  })

  it('points to the Alerts page when there are no channels yet', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/monitors': { body: [listItem(apiMonitor())] },
      'GET /api/orgs/org-acme/alert-channels': { body: [] },
    })
    renderAt('/monitors?new=1', { demo: false })

    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('link', { name: 'Add one' })).toHaveAttribute('href', '/alerts')
  })
})

describe('live incident updates', () => {
  it('refreshes the lists, the open incident and the dashboard counts', () => {
    const client = new QueryClient()
    client.setQueryData(incidentKeys.list('org-acme', 'open'), { pages: [], pageParams: [] })
    client.setQueryData(incidentKeys.one('inc-1'), null)
    client.setQueryData(incidentKeys.summary('org-acme'), { up: 1 })

    applyIncidentChanged(client, { incidentId: 'inc-1', monitorId: 'mon-1', title: 'Public API is down' })

    expect(client.getQueryState(incidentKeys.list('org-acme', 'open'))?.isInvalidated).toBe(true)
    expect(client.getQueryState(incidentKeys.one('inc-1'))?.isInvalidated).toBe(true)
    expect(client.getQueryState(incidentKeys.summary('org-acme'))?.isInvalidated).toBe(true)
  })
})

describe('Incidents on a monitor', () => {
  it('lists that monitor\'s incidents and links to them', async () => {
    const monitor = {
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
      state: 'Down',
      stateChangedAt: '2026-11-08T10:00:00Z',
      lastCheckedAt: new Date().toISOString(),
      lastResponseMs: 100,
      lastStatusCode: 503,
      lastErrorType: null,
      lastErrorMessage: null,
      badgeEnabled: false,
      createdAt: '2026-11-01T10:00:00Z',
      alertChannelIds: [],
    }
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/monitors/mon-1': { body: monitor },
      'GET /api/monitors/mon-1/stats?range=24h': {
        body: { range: '24h', checks: 0, uptimePercent: null, avgMs: null, p50Ms: null, p95Ms: null, p99Ms: null, uptime: { last24Hours: null, last7Days: null, last30Days: null }, series: [] },
      },
      'GET /api/monitors/mon-1/checks?limit=25': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/incidents?state=all&limit=20&monitorId=mon-1': { body: { items: [incident()], nextBefore: null } },
    })
    const user = userEvent.setup()
    renderAt('/monitors/mon-1', { demo: false })

    await user.click(await screen.findByRole('tab', { name: 'Incidents (1)' }))

    expect(await screen.findByRole('link', { name: 'Public API is down' })).toHaveAttribute('href', '/incidents/inc-1')
    expect(server.called('GET /api/orgs/org-acme/incidents?state=all&limit=20&monitorId=mon-1')).toHaveLength(1)
  })
})
