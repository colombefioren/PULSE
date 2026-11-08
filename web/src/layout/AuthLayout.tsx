import { Link, Outlet } from 'react-router'
import { Logo } from '@/components/Logo'
import { RingGauge } from '@/components/charts/RingGauge'
import { Sparkline } from '@/components/charts/Sparkline'
import { StatusPill } from '@/components/StatusPill'
import { ThemeToggle } from '@/components/ThemeToggle'
import { useMonitors, useSummary } from '@/data/queries'
import { formatMs } from '@/lib/stats'
import * as Tooltip from '@radix-ui/react-tooltip'

/** Live preview of the product, drawn from the demo workspace, for the left side of the auth screens. */
function Preview() {
  const summary = useSummary()
  const monitors = useMonitors()
  const api = monitors.data?.find((m) => m.id === 'mon-api')
  const auth = monitors.data?.find((m) => m.id === 'mon-auth')

  return (
    <div aria-hidden className="relative mt-auto hidden h-[340px] md:block">
      <div className="rise absolute left-0 top-0 w-[260px] rounded-card bg-panel-2 p-5" style={{ '--i': 2 } as React.CSSProperties}>
        <p className="text-sm text-panel-muted">Uptime, last 24 hours</p>
        <div className="mt-3 flex items-center gap-4">
          <RingGauge
            value={summary.data?.uptime24h ?? 99.9}
            label="Uptime"
            size={96}
            stroke={10}
            color="var(--brand)"
            track="rgb(255 255 255 / 14%)"
            textColor="var(--panel-text)"
          />
          <div className="mono text-xs text-panel-muted">
            <div>{summary.data?.up ?? 0} up</div>
            <div>{summary.data?.down ?? 0} down</div>
          </div>
        </div>
      </div>

      {api && (
        <div className="rise absolute right-0 top-10 w-[270px] rounded-card bg-panel-2 p-5" style={{ '--i': 3 } as React.CSSProperties}>
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">{api.name}</span>
            <StatusPill status={api.status} />
          </div>
          <div className="mt-3">
            <Sparkline points={api.spark} width={230} height={48} color="var(--brand)" label="" />
          </div>
          <p className="mono mt-2 text-xs text-panel-muted">{formatMs(api.lastResponseMs)}</p>
        </div>
      )}

      {auth && (
        <div className="rise absolute bottom-0 left-10 w-[300px] rounded-card bg-brand p-5 text-brand-ink" style={{ '--i': 4 } as React.CSSProperties}>
          <p className="text-xs font-semibold uppercase tracking-wider">Incident opened</p>
          <p className="mt-1 text-base font-semibold">{auth.name} is failing health checks</p>
          <p className="mono mt-1 text-xs">3 failed checks in a row. Team alerted by email and Slack.</p>
        </div>
      )}
    </div>
  )
}

/** Split layout shared by sign in and sign up. */
export function AuthLayout() {
  return (
    <Tooltip.Provider>
      <div className="min-h-dvh bg-canvas p-3 md:p-4">
        <div className="mx-auto grid min-h-[calc(100dvh-1.5rem)] max-w-[1400px] gap-3 md:min-h-[calc(100dvh-2rem)] md:grid-cols-[1.05fr_1fr] md:gap-4">
          <aside className="hidden flex-col rounded-shell bg-panel p-10 text-panel-fg md:flex">
            <Link to="/" aria-label="Pulse home">
              <Logo height={28} iconGap={12} />
            </Link>
            <h2 className="mt-14 max-w-md text-2xl font-semibold leading-tight tracking-tight">
              Know before your customers do.
            </h2>
            <p className="mt-3 max-w-sm text-base text-panel-muted">
              Checks every 30 seconds, an incident the moment three in a row fail, and a status page that tells the truth.
            </p>
            <Preview />
          </aside>

          <main className="relative flex flex-col rounded-shell bg-bg px-6 py-8 md:px-12">
            <div className="flex items-center justify-between">
              <Link to="/" aria-label="Pulse home" className="md:invisible">
                <Logo height={24} iconGap={10} />
              </Link>
              <ThemeToggle />
            </div>
            <div className="mx-auto flex w-full max-w-[400px] flex-1 flex-col justify-center py-10">
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </Tooltip.Provider>
  )
}
