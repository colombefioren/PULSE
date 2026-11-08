import { dayState, summarize, uptimePercent, type SeriesPoint } from '@/lib/stats'
import { hashString, mulberry32 } from './rng'
import type {
  ChartRange,
  DashboardSummary,
  DayUptime,
  Incident,
  Monitor,
  MonitorStatus,
  StatusComponent,
  StatusPage,
} from './types'

/**
 * The demo workspace.
 *
 * Until the API exists this is the only data source, behind the same functions the real client will
 * expose, so screens do not change when it is swapped. It is deterministic (seeded) and "ticks":
 * every call to `tick()` adds a fresh check to each active monitor, which is what makes live values
 * update in place.
 */

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

interface Blueprint {
  id: string
  name: string
  url: string
  method: Monitor['method']
  intervalS: number
  baseMs: number
  jitter: number
  status: MonitorStatus
  group: string
  /** 90 day uptime target; the generator places outages to roughly match it. */
  uptime90d: number
}

const BLUEPRINTS: Blueprint[] = [
  { id: 'mon-api', name: 'Public API', url: 'https://api.pulse-demo.dev/v1/health', method: 'GET', intervalS: 30, baseMs: 92, jitter: 0.18, status: 'up', group: 'Core', uptime90d: 99.98 },
  { id: 'mon-auth', name: 'Auth service', url: 'https://auth.pulse-demo.dev/healthz', method: 'GET', intervalS: 30, baseMs: 128, jitter: 0.22, status: 'down', group: 'Core', uptime90d: 99.71 },
  { id: 'mon-checkout', name: 'Checkout', url: 'https://pay.pulse-demo.dev/ping', method: 'GET', intervalS: 30, baseMs: 240, jitter: 0.35, status: 'up', group: 'Core', uptime90d: 99.92 },
  { id: 'mon-web', name: 'Marketing site', url: 'https://pulse-demo.dev', method: 'GET', intervalS: 60, baseMs: 176, jitter: 0.2, status: 'up', group: 'Web', uptime90d: 100 },
  { id: 'mon-docs', name: 'Docs', url: 'https://docs.pulse-demo.dev', method: 'GET', intervalS: 300, baseMs: 142, jitter: 0.16, status: 'up', group: 'Web', uptime90d: 99.99 },
  { id: 'mon-hooks', name: 'Webhook receiver', url: 'https://hooks.pulse-demo.dev/in', method: 'POST', intervalS: 60, baseMs: 64, jitter: 0.25, status: 'up', group: 'Core', uptime90d: 99.95 },
  { id: 'mon-cdn', name: 'Asset CDN', url: 'https://cdn.pulse-demo.dev/health', method: 'HEAD', intervalS: 60, baseMs: 38, jitter: 0.3, status: 'up', group: 'Web', uptime90d: 100 },
  { id: 'mon-admin', name: 'Legacy admin', url: 'https://admin.pulse-demo.dev', method: 'GET', intervalS: 600, baseMs: 310, jitter: 0.2, status: 'paused', group: 'Web', uptime90d: 98.4 },
]

/** Response time at a moment, with a daily rhythm, noise and the occasional spike. */
function responseAt(bp: Blueprint, t: number, rand: () => number): number | null {
  const hour = (t % DAY) / HOUR
  const rhythm = 1 + 0.12 * Math.sin(((hour - 14) / 24) * Math.PI * 2)
  const noise = 1 + (rand() - 0.5) * 2 * bp.jitter
  const spike = rand() < 0.02 ? 1.9 + rand() : 1
  return Math.max(8, Math.round(bp.baseMs * rhythm * noise * spike))
}

/** Where outages sit, as offsets back from now in ms, for monitors that are currently failing. */
function currentOutageStart(bp: Blueprint, now: number): number | null {
  return bp.status === 'down' ? now - 14 * MINUTE : null
}

const RANGE_POINTS: Record<ChartRange, { count: number; step: number }> = {
  '1h': { count: 60, step: MINUTE },
  '24h': { count: 96, step: 15 * MINUTE },
  '7d': { count: 168, step: HOUR },
  '30d': { count: 120, step: 6 * HOUR },
}

function buildSeries(bp: Blueprint, range: ChartRange, now: number): SeriesPoint[] {
  const { count, step } = RANGE_POINTS[range]
  const rand = mulberry32(hashString(`${bp.id}:${range}`))
  const outageStart = currentOutageStart(bp, now)
  const points: SeriesPoint[] = []

  for (let i = count - 1; i >= 0; i -= 1) {
    const t = now - i * step
    if (bp.status === 'paused') continue
    // The current outage, plus one past dip in the 7d and 30d views so the charts have a story.
    const inCurrentOutage = outageStart !== null && t >= outageStart
    const inPastDip = (range === '7d' || range === '30d') && bp.id === 'mon-checkout' && t > now - 3 * DAY && t < now - 3 * DAY + 2 * HOUR
    points.push({ t, ms: inCurrentOutage || inPastDip ? null : responseAt(bp, t, rand) })
  }
  return points
}

function buildDays(bp: Blueprint, now: number): DayUptime[] {
  const rand = mulberry32(hashString(`${bp.id}:days`))
  const lossBudget = 100 - bp.uptime90d
  const days: DayUptime[] = []

  for (let i = 89; i >= 0; i -= 1) {
    const date = new Date(now - i * DAY).toISOString().slice(0, 10)
    // Spread the loss over a handful of bad days instead of nudging every day a little.
    const bad = rand() < Math.min(0.12, (lossBudget / 1.6) / 30)
    let pct: number | null = 100
    if (bp.status === 'paused' && i < 20) pct = null
    else if (bad) pct = Math.round((100 - (0.4 + rand() * 4.5)) * 100) / 100
    if (bp.status === 'down' && i === 0) pct = 99.0
    days.push({
      date,
      uptimePct: pct,
      downtimeS: pct === null ? 0 : Math.round(((100 - pct) / 100) * 86400),
      incidents: pct !== null && pct < 99.9 ? 1 : 0,
    })
  }
  return days
}

function buildIncidents(now: number): Incident[] {
  return [
    {
      id: 'inc-auth-now',
      monitorId: 'mon-auth',
      title: 'Auth service is failing health checks',
      severity: 'major',
      status: 'investigating',
      startedAt: now - 14 * MINUTE,
      acknowledgedAt: null,
      resolvedAt: null,
      isManual: false,
      updates: [
        { status: 'investigating', message: 'Three consecutive checks failed with a connection timeout after 10s.', at: now - 14 * MINUTE },
      ],
    },
    {
      id: 'inc-checkout-slow',
      monitorId: 'mon-checkout',
      title: 'Checkout responses slower than usual',
      severity: 'degraded',
      status: 'resolved',
      startedAt: now - 3 * DAY,
      acknowledgedAt: now - 3 * DAY + 6 * MINUTE,
      resolvedAt: now - 3 * DAY + 2 * HOUR,
      isManual: false,
      updates: [
        { status: 'investigating', message: 'Checks started timing out against the payment gateway.', at: now - 3 * DAY },
        { status: 'identified', message: 'The gateway is rate limiting us after a retry storm. Backing off retries.', at: now - 3 * DAY + 40 * MINUTE },
        { status: 'monitoring', message: 'Retries are throttled and latency is back to normal. Watching for an hour.', at: now - 3 * DAY + 90 * MINUTE },
        { status: 'resolved', message: 'No failed checks for an hour. Resolved.', at: now - 3 * DAY + 2 * HOUR },
      ],
    },
    {
      id: 'inc-maint',
      monitorId: null,
      title: 'Scheduled database maintenance',
      severity: 'maintenance',
      status: 'resolved',
      startedAt: now - 11 * DAY,
      acknowledgedAt: null,
      resolvedAt: now - 11 * DAY + 45 * MINUTE,
      isManual: true,
      updates: [
        { status: 'investigating', message: 'Maintenance window started. Writes may be briefly unavailable.', at: now - 11 * DAY },
        { status: 'resolved', message: 'Maintenance finished. All services are back to normal.', at: now - 11 * DAY + 45 * MINUTE },
      ],
    },
    {
      id: 'inc-cdn',
      monitorId: 'mon-cdn',
      title: 'Asset CDN returned 502 from one region',
      severity: 'partial',
      status: 'resolved',
      startedAt: now - 19 * DAY,
      acknowledgedAt: now - 19 * DAY + 4 * MINUTE,
      resolvedAt: now - 19 * DAY + 28 * MINUTE,
      isManual: false,
      updates: [
        { status: 'investigating', message: 'Edge nodes in one region answered 502.', at: now - 19 * DAY },
        { status: 'resolved', message: 'The provider rerouted traffic. Resolved.', at: now - 19 * DAY + 28 * MINUTE },
      ],
    },
  ]
}

class DemoBackend {
  private readonly bootedAt = Date.now()
  private monitors: Monitor[]
  private incidents: Incident[]
  private ticks = 0

  constructor() {
    const now = this.bootedAt
    this.incidents = buildIncidents(now)
    this.monitors = BLUEPRINTS.map((bp) => {
      const spark = buildSeries(bp, '24h', now).filter((_, i) => i % 2 === 0)
      const last = spark[spark.length - 1]
      const days = buildDays(bp, now)
      const avg = (n: number) => {
        const slice = days.slice(-n).flatMap((d) => (d.uptimePct === null ? [] : [d.uptimePct]))
        return slice.length ? Math.round((slice.reduce((a, b) => a + b, 0) / slice.length) * 10) / 10 : 100
      }
      const s = summarize(spark)
      return {
        id: bp.id,
        name: bp.name,
        url: bp.url,
        method: bp.method,
        intervalS: bp.intervalS,
        status: bp.status,
        lastResponseMs: last?.ms ?? null,
        lastCheckedAt: now - Math.floor(Math.random() * 20_000),
        uptime24h: bp.status === 'down' ? Math.min(s.uptimePct, 99.0) : s.uptimePct,
        uptime7d: avg(7),
        uptime30d: avg(30),
        uptime90d: avg(90),
        spark,
      }
    })
  }

  /** One round of checks: each active monitor gets a new point, so live values change. */
  tick(): void {
    this.ticks += 1
    const now = Date.now()
    this.monitors = this.monitors.map((monitor) => {
      if (monitor.status === 'paused') return monitor
      const bp = BLUEPRINTS.find((b) => b.id === monitor.id)
      if (!bp) return monitor
      const rand = mulberry32(hashString(`${monitor.id}:tick:${this.ticks}`))
      const ms = monitor.status === 'down' ? null : responseAt(bp, now, rand)
      const spark = [...monitor.spark.slice(1), { t: now, ms }]
      return { ...monitor, lastResponseMs: ms, lastCheckedAt: now, spark }
    })
  }

  private lastTickAt = 0

  /** Runs a round of checks at most once per interval, however many screens ask. */
  tickIfDue(minGapMs = 5000): void {
    const now = Date.now()
    if (now - this.lastTickAt < minGapMs) return
    this.lastTickAt = now
    this.tick()
  }

  listMonitors(): Monitor[] {
    return this.monitors
  }

  getMonitor(id: string): Monitor | undefined {
    return this.monitors.find((monitor) => monitor.id === id)
  }

  series(id: string, range: ChartRange): SeriesPoint[] {
    const bp = BLUEPRINTS.find((b) => b.id === id)
    return bp ? buildSeries(bp, range, this.bootedAt) : []
  }

  listIncidents(): Incident[] {
    return this.incidents
  }

  acknowledge(id: string): Incident | undefined {
    const now = Date.now()
    this.incidents = this.incidents.map((incident) =>
      incident.id === id && incident.acknowledgedAt === null ? { ...incident, acknowledgedAt: now } : incident,
    )
    return this.incidents.find((incident) => incident.id === id)
  }

  summary(): DashboardSummary {
    const active = this.monitors.filter((m) => m.status !== 'paused')
    const avgs = active.flatMap((m) => (m.lastResponseMs === null ? [] : [m.lastResponseMs]))
    return {
      up: this.monitors.filter((m) => m.status === 'up').length,
      down: this.monitors.filter((m) => m.status === 'down').length,
      paused: this.monitors.filter((m) => m.status === 'paused').length,
      openIncidents: this.incidents.filter((i) => i.status !== 'resolved').length,
      uptime24h: uptimePercent(
        active.reduce((sum, m) => sum + m.spark.filter((p) => p.ms !== null).length, 0),
        active.reduce((sum, m) => sum + m.spark.length, 0),
      ),
      avgResponseMs: avgs.length ? Math.round(avgs.reduce((a, b) => a + b, 0) / avgs.length) : 0,
    }
  }

  statusPage(slug: string): StatusPage | undefined {
    if (slug !== 'pulse-demo') return undefined
    const now = this.bootedAt
    const visible = BLUEPRINTS.filter((bp) => bp.status !== 'paused')
    const components: StatusComponent[] = visible.map((bp) => {
      const days = buildDays(bp, now)
      const live = this.monitors.find((m) => m.id === bp.id)
      const state = live?.status === 'down' ? 'down' : dayState(days[days.length - 1]?.uptimePct ?? null) === 'degraded' ? 'degraded' : 'up'
      return { id: bp.id, name: bp.name, group: bp.group, status: state, uptime90d: live?.uptime90d ?? bp.uptime90d, days }
    })
    return {
      slug,
      title: 'Pulse Demo status',
      accent: '#fac303',
      footer: 'Questions about an incident? Reply to any update email.',
      showResponseTimes: true,
      components,
      activeIncidents: this.incidents.filter((i) => i.status !== 'resolved'),
      history: this.incidents.filter((i) => i.status === 'resolved'),
    }
  }
}

export const demo = new DemoBackend()
