import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type {
  ApiIncidentDetail,
  ApiMonitor,
  ApiMonitorListItem,
  ApiPublicStatus,
  ApiStatusPage,
  Me,
} from '@/api/types'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const as = (role: 'Owner' | 'Admin' | 'Viewer'): Me => ({ ...ana, organizations: [{ ...ana.organizations[0]!, role }] })

const statusPage = (over: Partial<ApiStatusPage> = {}): ApiStatusPage => ({
  id: 'sp-1',
  organizationId: 'org-acme',
  slug: 'acme-status',
  title: 'Acme status',
  accentColor: '#fac303',
  footer: '',
  showResponseTimes: true,
  logoUrl: null,
  subscriberCount: 0,
  components: [],
  createdAt: '2026-11-01T10:00:00Z',
  ...over,
})

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

const monitorList = (...monitors: ApiMonitor[]): { body: ApiMonitorListItem[] } => ({
  body: monitors.map((monitor) => ({ monitor, uptime24h: 100, sparkline: [] })),
})

const day = (offset: number, uptimePct: number | null = 100) => ({
  date: new Date(Date.UTC(2026, 10, 8) - offset * 86_400_000).toISOString().slice(0, 10),
  uptimePct,
  downtimeSeconds: uptimePct !== null && uptimePct < 100 ? 600 : 0,
  incidents: uptimePct !== null && uptimePct < 100 ? 1 : 0,
})

const publicStatus = (over: Partial<ApiPublicStatus> = {}): ApiPublicStatus => ({
  slug: 'acme-status',
  title: 'Acme status',
  logoUrl: null,
  accentColor: '#fac303',
  footer: 'Questions? ops@acme.dev',
  showResponseTimes: true,
  generatedAt: '2026-11-08T10:00:00Z',
  components: [
    {
      id: 'c-1',
      name: 'Website',
      group: 'Core',
      status: 'up',
      uptime90d: 99.95,
      avgResponseMs: 120,
      days: Array.from({ length: 90 }, (_, i) => day(89 - i)),
    },
    {
      id: 'c-2',
      name: 'Checkout',
      group: null,
      status: 'down',
      uptime90d: null,
      avgResponseMs: null,
      days: Array.from({ length: 90 }, (_, i) => day(89 - i, null)),
    },
  ],
  activeIncidents: [
    {
      id: 'i-1',
      title: 'Issue affecting Checkout',
      severity: 'MajorOutage',
      status: 'Investigating',
      startedAt: new Date(Date.now() - 600_000).toISOString(),
      resolvedAt: null,
      components: ['Checkout'],
      updates: [{ status: 'Investigating', message: 'We are investigating an issue affecting Checkout.', at: new Date(Date.now() - 600_000).toISOString() }],
    },
  ],
  history: [],
  ...over,
})

describe('Status pages list', () => {
  it('lists the team’s pages with counts and links', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/status-pages': {
        body: [
          statusPage({ subscriberCount: 3, components: [{ id: 'c-1', name: 'Website', groupName: null, sortOrder: 0, monitorIds: ['mon-1'] }] }),
          statusPage({ id: 'sp-2', slug: 'internal', title: 'Internal tools' }),
        ],
      },
    })
    renderAt('/status-pages', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Acme status' })).toBeInTheDocument()
    expect(screen.getByText(/1 component · 3 subscribers/)).toBeInTheDocument()
    expect(screen.getByText(/No components yet · 0 subscribers/)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /View public page/ })[0]).toHaveAttribute('href', '/status/acme-status')
    expect(screen.getByRole('link', { name: 'Edit Acme status' })).toHaveAttribute('href', '/status-pages/sp-1')
  })

  it('invites you to create the first page', async () => {
    mockApi({ 'GET /api/auth/me': { body: ana }, 'GET /api/orgs/org-acme/status-pages': { body: [] } })
    renderAt('/status-pages', { demo: false })

    expect(await screen.findByRole('heading', { name: 'No status pages yet' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Create status page/ }).length).toBeGreaterThan(0)
  })

  it('lets a viewer look but not create', async () => {
    mockApi({ 'GET /api/auth/me': { body: as('Viewer') }, 'GET /api/orgs/org-acme/status-pages': { body: [statusPage()] } })
    renderAt('/status-pages', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Acme status' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Create status page/ })).not.toBeInTheDocument()
  })

  it('shows the simulated page in the demo', async () => {
    renderAt('/status-pages')

    expect(await screen.findByRole('heading', { name: 'Pulse Demo status' })).toBeInTheDocument()
    expect(screen.getByText(/demo status page/)).toBeInTheDocument()
  })

  it('creates a page from a title, suggesting the address, and opens its editor', async () => {
    const user = userEvent.setup()
    const created = statusPage({ id: 'sp-9', slug: 'acme-platform-status', title: 'Acme Platform Status' })
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/status-pages': { body: [] },
      'POST /api/orgs/org-acme/status-pages': { status: 201, body: created },
      'GET /api/status-pages/sp-9': { body: created },
      'GET /api/orgs/org-acme/monitors': monitorList(),
    })
    const { router } = renderAt('/status-pages', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Create status page/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Create page' }))
    expect(await within(dialog).findByText('Enter a title')).toBeInTheDocument()
    expect(within(dialog).getByText('Choose an address for the page')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Title'), 'Acme Platform Status')
    expect(within(dialog).getByLabelText('Address')).toHaveValue('acme-platform-status')
    await user.click(within(dialog).getByRole('button', { name: 'Create page' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/status-pages/sp-9'))
    expect(server.called('POST /api/orgs/org-acme/status-pages')[0]?.body).toMatchObject({
      slug: 'acme-platform-status',
      title: 'Acme Platform Status',
      accentColor: null,
      showResponseTimes: true,
    })
  })

  it('stops an address the app already uses before asking the server', async () => {
    const user = userEvent.setup()
    const server = mockApi({ 'GET /api/auth/me': { body: ana }, 'GET /api/orgs/org-acme/status-pages': { body: [] } })
    renderAt('/status-pages', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Create status page/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Title'), 'Admin')
    await user.click(within(dialog).getByRole('button', { name: 'Create page' }))

    expect(await within(dialog).findByText(/used by the app/)).toBeInTheDocument()
    expect(server.called('POST /api/orgs/org-acme/status-pages')).toHaveLength(0)
  })

  it('shows the server’s answer when the address is taken', async () => {
    const user = userEvent.setup()
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/status-pages': { body: [] },
      'POST /api/orgs/org-acme/status-pages': { status: 409, body: problem('slug_taken', 'That address is already used by another page.', { slug: ['That address is already taken'] }) },
    })
    renderAt('/status-pages', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Create status page/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Title'), 'Acme status')
    await user.click(within(dialog).getByRole('button', { name: 'Create page' }))

    expect(await within(dialog).findByText('That address is already taken')).toBeInTheDocument()
  })
})

describe('Status page editor', () => {
  const page = statusPage({
    footer: 'Contact us',
    components: [
      { id: 'c-1', name: 'Website', groupName: 'Core', sortOrder: 0, monitorIds: ['mon-1'] },
      { id: 'c-2', name: 'Checkout', groupName: null, sortOrder: 1, monitorIds: ['mon-2'] },
    ],
  })
  const base = () => ({
    'GET /api/auth/me': { body: ana },
    'GET /api/status-pages/sp-1': { body: page },
    'GET /api/orgs/org-acme/monitors': monitorList(apiMonitor(), apiMonitor({ id: 'mon-2', name: 'Checkout flow' })),
  })

  it('shows the settings, components with their monitors, and the subscriber count', async () => {
    mockApi(base())
    renderAt('/status-pages/sp-1', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Acme status' })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Acme status')
    expect(screen.getByLabelText('Address')).toHaveValue('acme-status')
    expect(screen.getByLabelText('Footer')).toHaveValue('Contact us')
    expect(await screen.findByText('Public API')).toBeInTheDocument()
    expect(screen.getByText('Checkout flow')).toBeInTheDocument()
    expect(screen.getByText('Core')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Subscribers' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /View public page/ })).toHaveAttribute('href', '/status/acme-status')
  })

  it('saves settings, sending a blank accent as the default', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...base(), 'PATCH /api/status-pages/sp-1': { body: statusPage({ title: 'Acme systems' }) } })
    renderAt('/status-pages/sp-1', { demo: false })

    const title = await screen.findByLabelText('Title')
    await user.clear(title)
    await user.type(title, 'Acme systems')
    await user.clear(screen.getByLabelText('Accent colour'))
    await user.click(screen.getByRole('checkbox', { name: 'Show average response times' }))
    await user.click(screen.getByRole('button', { name: 'Save settings' }))

    await waitFor(() => expect(server.called('PATCH /api/status-pages/sp-1')).toHaveLength(1))
    expect(server.called('PATCH /api/status-pages/sp-1')[0]).toMatchObject({
      body: { slug: 'acme-status', title: 'Acme systems', accentColor: null, footer: 'Contact us', showResponseTimes: false },
      csrf: 'test-token',
    })
    expect(await screen.findByText(/Saved/)).toBeInTheDocument()
  })

  it('checks the accent colour and the address before saving', async () => {
    const user = userEvent.setup()
    const server = mockApi(base())
    renderAt('/status-pages/sp-1', { demo: false })

    const accent = await screen.findByLabelText('Accent colour')
    await user.clear(accent)
    await user.type(accent, 'yellow')
    await user.clear(screen.getByLabelText('Address'))
    await user.type(screen.getByLabelText('Address'), 'Bad Address')
    await user.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(await screen.findByText('Use a colour like #fac303')).toBeInTheDocument()
    expect(screen.getByText(/Use lower case letters/)).toBeInTheDocument()
    expect(server.called('PATCH /api/status-pages/sp-1')).toHaveLength(0)
  })

  it('warns when text on the chosen accent would be hard to read', async () => {
    const user = userEvent.setup()
    mockApi(base())
    renderAt('/status-pages/sp-1', { demo: false })

    const accent = await screen.findByLabelText('Accent colour')
    await user.clear(accent)
    await user.type(accent, '#8b5cf6')

    expect(await screen.findByText(/hard to read/)).toBeInTheDocument()
  })

  it('uploads a logo as the raw file and shows the result', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base(),
      'PUT /api/status-pages/sp-1/logo': { body: statusPage({ logoUrl: '/api/public/status/acme-status/logo?v=1' }) },
    })
    renderAt('/status-pages/sp-1', { demo: false })

    await screen.findByRole('heading', { name: 'Acme status' })
    const file = new File([new Uint8Array(2048)], 'logo.png', { type: 'image/png' })
    await user.upload(screen.getByLabelText('Choose a logo file'), file)

    await waitFor(() => expect(server.called('PUT /api/status-pages/sp-1/logo')).toHaveLength(1))
    expect(server.called('PUT /api/status-pages/sp-1/logo')[0]).toMatchObject({ csrf: 'test-token', contentType: 'image/png', fileBytes: 2048 })
    expect(await screen.findByRole('img', { name: 'Logo of Acme status' })).toHaveAttribute('src', '/api/public/status/acme-status/logo?v=1')
  })

  it('refuses a logo that is the wrong type or too big without uploading it', async () => {
    const user = userEvent.setup({ applyAccept: false })
    const server = mockApi(base())
    renderAt('/status-pages/sp-1', { demo: false })

    await screen.findByRole('heading', { name: 'Acme status' })
    const input = screen.getByLabelText('Choose a logo file')
    await user.upload(input, new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' }))
    expect(await screen.findByText('Use a PNG, JPEG or WebP image.')).toBeInTheDocument()

    await user.upload(input, new File([new Uint8Array(300 * 1024)], 'big.png', { type: 'image/png' }))
    expect(await screen.findByText(/Use an image of 256 KB or less/)).toBeInTheDocument()
    expect(server.called('PUT /api/status-pages/sp-1/logo')).toHaveLength(0)
  })

  it('removes a logo', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base(),
      'GET /api/status-pages/sp-1': { body: statusPage({ logoUrl: '/api/public/status/acme-status/logo?v=2' }) },
      'DELETE /api/status-pages/sp-1/logo': { body: statusPage() },
    })
    renderAt('/status-pages/sp-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Remove logo' }))

    await waitFor(() => expect(server.called('DELETE /api/status-pages/sp-1/logo')).toHaveLength(1))
    await waitFor(() => expect(screen.queryByRole('img', { name: /Logo of/ })).not.toBeInTheDocument())
  })

  it('adds a component from chosen monitors', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base(),
      'POST /api/status-pages/sp-1/components': { status: 201, body: { id: 'c-3', name: 'API', groupName: 'Core', sortOrder: 2, monitorIds: ['mon-1'] } },
    })
    renderAt('/status-pages/sp-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: /Add component/ }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Add component' }))
    expect(await within(dialog).findByText('Enter a name')).toBeInTheDocument()
    expect(within(dialog).getByText('Choose at least one monitor')).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Name'), 'API')
    await user.type(within(dialog).getByLabelText('Group (optional)'), 'Core')
    await user.click(await within(dialog).findByRole('checkbox', { name: 'Public API' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add component' }))

    await waitFor(() => expect(server.called('POST /api/status-pages/sp-1/components')).toHaveLength(1))
    expect(server.called('POST /api/status-pages/sp-1/components')[0]?.body).toEqual({ name: 'API', groupName: 'Core', monitorIds: ['mon-1'] })
  })

  it('edits a component and keeps its monitors ticked', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base(),
      'PATCH /api/status-pages/sp-1/components/c-1': { body: { id: 'c-1', name: 'Marketing site', groupName: 'Core', sortOrder: 0, monitorIds: ['mon-1'] } },
    })
    renderAt('/status-pages/sp-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Edit Website' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('checkbox', { name: 'Public API' })).toBeChecked()
    expect(within(dialog).getByRole('checkbox', { name: 'Checkout flow' })).not.toBeChecked()
    const name = within(dialog).getByLabelText('Name')
    await user.clear(name)
    await user.type(name, 'Marketing site')
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(server.called('PATCH /api/status-pages/sp-1/components/c-1')).toHaveLength(1))
    expect(server.called('PATCH /api/status-pages/sp-1/components/c-1')[0]?.body).toEqual({ name: 'Marketing site', groupName: 'Core', monitorIds: ['mon-1'] })
  })

  it('moves a component down and saves the new order', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...base(), 'PUT /api/status-pages/sp-1/components/order': { status: 204 } })
    renderAt('/status-pages/sp-1', { demo: false })

    expect(await screen.findByRole('button', { name: 'Move Website up' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Move Checkout down' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Move Website down' }))

    await waitFor(() => expect(server.called('PUT /api/status-pages/sp-1/components/order')).toHaveLength(1))
    expect(server.called('PUT /api/status-pages/sp-1/components/order')[0]?.body).toEqual({ componentIds: ['c-2', 'c-1'] })
  })

  it('asks before removing a component', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...base(), 'DELETE /api/status-pages/sp-1/components/c-2': { status: 204 } })
    renderAt('/status-pages/sp-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delete Checkout' }))
    const dialog = await screen.findByRole('alertdialog').catch(() => screen.findByRole('dialog'))
    expect(server.called('DELETE /api/status-pages/sp-1/components/c-2')).toHaveLength(0)
    await user.click(within(dialog).getByRole('button', { name: 'Remove component' }))

    await waitFor(() => expect(server.called('DELETE /api/status-pages/sp-1/components/c-2')).toHaveLength(1))
  })

  it('deletes the page after confirming and returns to the list', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      ...base(),
      'DELETE /api/status-pages/sp-1': { status: 204 },
      'GET /api/orgs/org-acme/status-pages': { body: [] },
    })
    const { router } = renderAt('/status-pages/sp-1', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delete status page' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete status page' }))

    await waitFor(() => expect(server.called('DELETE /api/status-pages/sp-1')).toHaveLength(1))
    await waitFor(() => expect(router.state.location.pathname).toBe('/status-pages'))
  })

  it('is read only for a viewer', async () => {
    mockApi({ ...base(), 'GET /api/auth/me': { body: as('Viewer') } })
    renderAt('/status-pages/sp-1', { demo: false })

    expect(await screen.findByText(/only Owners and Admins can change it/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save settings' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Add component/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete status page' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeDisabled()
  })

  it('says so when the page is not found', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/status-pages/nope': { status: 404, body: problem('status_page_not_found', 'That status page does not exist.') },
    })
    renderAt('/status-pages/nope', { demo: false })

    expect(await screen.findByRole('heading', { name: 'Status page not found' })).toBeInTheDocument()
  })
})

describe('Public status page for a real account', () => {
  it('draws the page from the API, grouped, with the incident and what it affects', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus() } })
    renderAt('/status/acme-status', { demo: false })

    expect(await screen.findByRole('heading', { level: 1, name: 'Acme status' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Core' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Services' })).toBeInTheDocument()
    expect(screen.getByText('99.95% uptime')).toBeInTheDocument()
    expect(screen.getByText('No data yet')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Issue affecting Checkout' })).toBeInTheDocument()
    expect(screen.getByText('Affects Checkout')).toBeInTheDocument()
    expect(screen.getByText('Questions? ops@acme.dev')).toBeInTheDocument()
    expect(screen.getByText(/Partial outage/)).toBeInTheDocument()
    expect(screen.getAllByRole('img', { name: /days of uptime/ })).toHaveLength(2)
  })

  it('shows averages instead of a chart, and hides them when the page asks to', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus() } })
    const { unmount } = renderAt('/status/acme-status', { demo: false })

    expect(await screen.findByText('120 ms average')).toBeInTheDocument()
    unmount()

    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus({ showResponseTimes: false }) } })
    renderAt('/status/acme-status', { demo: false })
    await screen.findByRole('heading', { level: 1, name: 'Acme status' })
    expect(screen.queryByText(/ms average/)).not.toBeInTheDocument()
  })

  it('shows the team’s logo and uses its accent on the button with readable text', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus({ logoUrl: '/api/public/status/acme-status/logo?v=1', accentColor: '#111827' }) } })
    renderAt('/status/acme-status', { demo: false })

    expect(await screen.findByRole('presentation', { hidden: true }).catch(() => null)).toBeDefined()
    const logo = document.querySelector('img[src="/api/public/status/acme-status/logo?v=1"]')
    expect(logo).not.toBeNull()
    const button = await screen.findByRole('button', { name: /Subscribe to updates/ })
    expect(button).toHaveStyle({ background: '#111827', color: '#ffffff' })
  })

  it('says all systems are operational when nothing is wrong', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus({ activeIncidents: [], components: [publicStatus().components[0]!] }) } })
    renderAt('/status/acme-status', { demo: false })

    expect(await screen.findByRole('heading', { name: 'All systems operational' })).toBeInTheDocument()
    expect(screen.getByText('No incidents in the last 30 days.')).toBeInTheDocument()
  })

  it('subscribes without a session or an antiforgery token', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/public/status/acme-status': { body: publicStatus() },
      'POST /api/public/status/acme-status/subscribe': { status: 202 },
    })
    renderAt('/status/acme-status', { demo: false })

    await user.type(await screen.findByLabelText('Email address'), 'ada@reader.dev')
    await user.click(screen.getByRole('button', { name: /Subscribe to updates/ }))

    expect(await screen.findByRole('status')).toHaveTextContent('Check ada@reader.dev to confirm')
    expect(server.called('POST /api/public/status/acme-status/subscribe')[0]).toMatchObject({ body: { email: 'ada@reader.dev' }, csrf: null })
    expect(server.called('GET /api/auth/csrf')).toHaveLength(0)
  })

  it('checks the address before sending and shows what the server says about it', async () => {
    const user = userEvent.setup()
    const server = mockApi({
      'GET /api/public/status/acme-status': { body: publicStatus() },
      'POST /api/public/status/acme-status/subscribe': { status: 429, body: problem('rate_limited', 'Too many requests. Try again in a minute.') },
    })
    renderAt('/status/acme-status', { demo: false })

    const input = await screen.findByLabelText('Email address')
    await user.type(input, 'nope')
    await user.click(screen.getByRole('button', { name: /Subscribe to updates/ }))
    expect(await screen.findByText('Enter an email address like name@company.com')).toBeInTheDocument()
    expect(server.called('POST /api/public/status/acme-status/subscribe')).toHaveLength(0)

    await user.clear(input)
    await user.type(input, 'ada@reader.dev')
    await user.click(screen.getByRole('button', { name: /Subscribe to updates/ }))
    expect(await screen.findByText('Too many requests. Try again in a minute.')).toBeInTheDocument()
  })

  it('tells people they are subscribed or unsubscribed when they come back from an email', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus() } })
    const { unmount } = renderAt('/status/acme-status?subscription=confirmed', { demo: false })
    expect(await screen.findByText(/You are subscribed/)).toBeInTheDocument()
    unmount()

    mockApi({ 'GET /api/public/status/acme-status': { body: publicStatus() } })
    renderAt('/status/acme-status?subscription=removed', { demo: false })
    expect(await screen.findByText(/You are unsubscribed/)).toBeInTheDocument()
  })

  it('says so plainly for an address that does not exist', async () => {
    mockApi({ 'GET /api/public/status/nothing': { status: 404, body: problem('status_page_not_found', 'That status page does not exist.') } })
    renderAt('/status/nothing', { demo: false })

    expect(await screen.findByRole('heading', { name: 'No status page here' })).toBeInTheDocument()
  })

  it('offers a retry when the page cannot be loaded', async () => {
    mockApi({ 'GET /api/public/status/acme-status': { status: 500, body: problem('server_error', 'Something went wrong.') } })
    renderAt('/status/acme-status', { demo: false })

    expect(await screen.findByText(/Could not load this status page/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('explains an old confirmation or unsubscribe link', async () => {
    renderAt('/subscription/invalid', { demo: false })

    expect(await screen.findByRole('heading', { name: 'That link no longer works' })).toBeInTheDocument()
  })
})

describe('Incidents and status page components', () => {
  const incidentDetail = (components: ApiIncidentDetail['components']): ApiIncidentDetail => ({
    incident: {
      id: 'inc-9',
      organizationId: 'org-acme',
      monitorId: null,
      monitorName: null,
      title: 'Card payments are slow',
      severity: 'PartialOutage',
      status: 'Investigating',
      startedAt: new Date(Date.now() - 600_000).toISOString(),
      acknowledgedAt: null,
      acknowledgedBy: null,
      resolvedAt: null,
      isManual: true,
    },
    updates: [{ id: 'u-1', kind: 'Opened', status: 'Investigating', message: 'Some payments time out.', author: 'Ana', createdAt: '2026-11-08T10:00:00Z' }],
    alerts: [],
    components,
  })

  it('offers the page components when declaring an incident and sends the ones picked', async () => {
    const user = userEvent.setup()
    const created = incidentDetail([{ id: 'c-2', name: 'Checkout', statusPageId: 'sp-1', statusPageTitle: 'Acme status' }])
    const server = mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/monitors': monitorList(),
      'GET /api/orgs/org-acme/status-pages': {
        body: [
          statusPage({
            components: [
              { id: 'c-1', name: 'Website', groupName: null, sortOrder: 0, monitorIds: ['mon-1'] },
              { id: 'c-2', name: 'Checkout', groupName: null, sortOrder: 1, monitorIds: ['mon-2'] },
            ],
          }),
        ],
      },
      'POST /api/orgs/org-acme/incidents': { status: 201, body: created },
      'GET /api/incidents/inc-9': { body: created },
    })
    renderAt('/incidents', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Declare incident/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Title'), 'Card payments are slow')
    await user.type(within(dialog).getByLabelText('What is happening'), 'Some payments time out.')
    expect(await within(dialog).findByText('Acme status')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('checkbox', { name: 'Checkout' }))
    await user.click(within(dialog).getByRole('button', { name: 'Declare incident' }))

    await waitFor(() => expect(server.called('POST /api/orgs/org-acme/incidents')).toHaveLength(1))
    expect(server.called('POST /api/orgs/org-acme/incidents')[0]?.body).toMatchObject({ componentIds: ['c-2'] })
  })

  it('does not show the component list when there are no status pages', async () => {
    const user = userEvent.setup()
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/orgs/org-acme/incidents?state=open&limit=20': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/incidents?state=resolved&limit=20': { body: { items: [], nextBefore: null } },
      'GET /api/orgs/org-acme/monitors': monitorList(),
      'GET /api/orgs/org-acme/status-pages': { body: [] },
    })
    renderAt('/incidents', { demo: false })

    await user.click((await screen.findAllByRole('button', { name: /Declare incident/ }))[0]!)
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).queryByText(/Show on your status pages/)).not.toBeInTheDocument()
  })

  it('shows on the incident which components customers see it under', async () => {
    mockApi({
      'GET /api/auth/me': { body: ana },
      'GET /api/incidents/inc-9': {
        body: incidentDetail([
          { id: 'c-2', name: 'Checkout', statusPageId: 'sp-1', statusPageTitle: 'Acme status' },
          { id: 'c-3', name: 'Billing', statusPageId: 'sp-1', statusPageTitle: 'Acme status' },
        ]),
      },
    })
    renderAt('/incidents/inc-9', { demo: false })

    expect(await screen.findByTestId('affected-components')).toHaveTextContent('Shown on your status page as Checkout, Billing')
  })

  it('leaves the line out when the incident is on no page', async () => {
    mockApi({ 'GET /api/auth/me': { body: ana }, 'GET /api/incidents/inc-9': { body: incidentDetail([]) } })
    renderAt('/incidents/inc-9', { demo: false })

    await screen.findByRole('heading', { name: 'Card payments are slow' })
    expect(screen.queryByTestId('affected-components')).not.toBeInTheDocument()
  })
})
