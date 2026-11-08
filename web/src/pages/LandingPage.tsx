import { ArrowRight, BellRing, Globe, Siren, Timer } from 'lucide-react'
import * as Tooltip from '@radix-ui/react-tooltip'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { ResponseChart } from '@/components/charts/ResponseChart'
import { RingGauge } from '@/components/charts/RingGauge'
import { Logo } from '@/components/Logo'
import { StatusPageView } from '@/components/StatusPageView'
import { ThemeToggle } from '@/components/ThemeToggle'
import { useGlobalSeries, useStatusPage, useSummary } from '@/data/queries'

const FEATURES: { icon: typeof Timer; title: string; body: string }[] = [
  {
    icon: Timer,
    title: 'Check every 30 seconds',
    body: 'Each monitor runs on its own clock, with jitter so checks never stampede, and never runs twice at once.',
  },
  {
    icon: Siren,
    title: 'Incidents without the noise',
    body: 'You choose how many failures open an incident and how many successes close it. One slow response is not a page at 3 a.m.',
  },
  {
    icon: BellRing,
    title: 'Alerts that arrive once',
    body: 'Email, Slack, Discord or a signed webhook. Every state change is retried with backoff until it is delivered.',
  },
  {
    icon: Globe,
    title: 'A status page that tells the truth',
    body: 'Ninety days of daily uptime, live incident updates and a response-time graph. No sign-in, fast to load.',
  },
]

function Heading({ eyebrow, children }: { eyebrow: string; children: ReactNode }) {
  return (
    <div className="max-w-2xl">
      <p className="mono text-xs uppercase tracking-[0.14em] text-subtle">{eyebrow}</p>
      <h2 className="mt-3 text-[clamp(28px,3.4vw,40px)] font-semibold leading-[1.1] tracking-[-0.02em]">{children}</h2>
    </div>
  )
}

/** The product, drawn with the same live components the dashboard uses. */
function ProductPreview() {
  const summary = useSummary()
  const series = useGlobalSeries('24h')

  return (
    <div className="relative" aria-label="Preview of the Pulse dashboard" role="group">
      <div className="card p-5 shadow-[0_30px_80px_-40px_rgba(11,11,12,0.35)]">
        <div className="mb-4 flex items-center justify-between">
          <p className="text-md">Response time, 24 hours</p>
          <span className="mono flex items-center gap-2 text-xs text-muted">
            <span className="status-dot" data-status="up" aria-hidden />
            Live
          </span>
        </div>
        {series.data ? (
          <ResponseChart series={series.data} range="24h" subject="All monitors" height={220} />
        ) : (
          <div className="skeleton h-[220px]" aria-label="Loading chart" />
        )}
      </div>

      <div className="card-plum absolute -bottom-8 -left-4 flex items-center gap-4 p-4 max-sm:hidden md:-left-10">
        {summary.data ? (
          <RingGauge
            value={summary.data.uptime24h}
            label="Uptime, 24 hours"
            size={92}
            stroke={10}
            textColor="var(--plum-text)"
          />
        ) : (
          <div className="skeleton size-[92px] rounded-full" aria-hidden />
        )}
        <div>
          <p className="text-sm">Uptime, 24 hours</p>
          {summary.data && (
            <p className="mono text-xs opacity-80">
              {summary.data.up} up · {summary.data.down} down
            </p>
          )}
        </div>
      </div>

      <div className="absolute -right-3 -top-5 max-w-[240px] rounded-card bg-brand p-4 text-brand-ink shadow-lg max-sm:hidden md:-right-8">
        <p className="mono text-[11px] font-medium uppercase tracking-[0.12em]">Incident opened</p>
        <p className="mt-1 text-base font-medium">Auth service is failing health checks</p>
        <p className="mt-1 text-xs opacity-80">Three failed checks in a row. Your team is alerted.</p>
      </div>
    </div>
  )
}

export default function LandingPage() {
  const statusPage = useStatusPage('pulse-demo')

  return (
    <Tooltip.Provider>
    <div className="min-h-dvh bg-canvas p-3 text-fg md:p-5">
      <div className="mx-auto max-w-[1280px]">
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-6 focus:top-6 focus:z-50 focus:rounded-full focus:bg-bg focus:px-4 focus:py-2"
        >
          Skip to content
        </a>

        <header className="sticky top-3 z-40 flex items-center justify-between rounded-full bg-panel px-5 py-3 shadow-[0_12px_32px_-16px_rgba(11,11,12,0.5)] md:top-5 md:px-7">
          <Link to="/" aria-label="Pulse home">
            <Logo height={26} iconGap={10} />
          </Link>
          <nav aria-label="Account" className="flex items-center gap-1 md:gap-2">
            <a href="#features" className="btn btn-ghost text-white/80 hover:bg-white/10 hover:text-white max-md:hidden">
              Features
            </a>
            <a href="#status" className="btn btn-ghost text-white/80 hover:bg-white/10 hover:text-white max-md:hidden">
              Status page
            </a>
            <Link to="/login" className="btn btn-ghost text-white hover:bg-white/10">
              Sign in
            </Link>
            <Link to="/dashboard" className="btn btn-primary">
              Try the demo
            </Link>
          </nav>
        </header>

        <main id="main" className="mt-3 md:mt-5">
          <section className="rounded-shell bg-bg px-6 pb-20 pt-14 md:px-14 md:pb-28 md:pt-20">
            <div className="grid items-center gap-16 lg:grid-cols-[1.15fr_1fr]">
              <div>
                <p className="mono inline-flex rounded-full bg-surface px-3 py-1.5 text-xs text-muted">
                  Uptime monitoring and public status pages
                </p>
                <h1 className="mt-6 text-[clamp(34px,4.4vw,56px)] font-semibold leading-[1.04] tracking-[-0.03em]">
                  Know when it breaks, before your customers do.
                </h1>
                <p className="mt-6 max-w-lg text-md text-muted">
                  Pulse checks your sites and APIs every 30 seconds, opens an incident when they stop answering, and
                  keeps a public page honest while you fix it.
                </p>
                <div className="mt-8 flex flex-wrap gap-3">
                  <Link to="/dashboard" className="btn btn-primary btn-lg">
                    Try the demo <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
                  </Link>
                  <Link to="/signup" className="btn btn-lg">
                    Sign up
                  </Link>
                </div>
                <p className="mt-4 text-sm text-subtle">The demo needs no account.</p>
              </div>

              <ProductPreview />
            </div>
          </section>

          <section id="features" aria-labelledby="features-heading" className="scroll-mt-24 px-3 pb-6 pt-20 md:px-10 md:pt-28">
            <div id="features-heading">
              <Heading eyebrow="How it works">From a failed check to a fixed incident, in four steps.</Heading>
            </div>
            <ul className="mt-10 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {FEATURES.map(({ icon: Icon, title, body }) => (
                <li key={title} className="card flex flex-col gap-4 p-6">
                  <span className="grid size-10 place-items-center rounded-full bg-bg">
                    <Icon size={18} strokeWidth={1.5} aria-hidden />
                  </span>
                  <h3 className="text-md font-medium">{title}</h3>
                  <p className="text-base text-muted">{body}</p>
                </li>
              ))}
            </ul>
          </section>

          <section id="status" aria-labelledby="status-heading" className="scroll-mt-24 rounded-shell bg-bg px-4 py-14 md:px-12 md:py-20">
            <div id="status-heading">
              <Heading eyebrow="Live preview">This is what your customers see.</Heading>
            </div>
            <p className="mt-4 max-w-xl text-md text-muted">
              A real status page running on the demo workspace right now. Hover the bars for a day, and watch the auth
              service.
            </p>
            <div className="mt-10 rounded-card bg-canvas/40 p-3 md:p-8">
              {statusPage.data ? (
                <StatusPageView page={statusPage.data} embedded />
              ) : (
                <div className="skeleton h-[480px]" aria-label="Loading status page" />
              )}
            </div>
          </section>

          <section className="mt-3 flex flex-wrap items-center justify-between gap-8 rounded-shell bg-panel px-8 py-14 text-white md:mt-5 md:px-14 md:py-16">
            <h2 className="max-w-md text-[clamp(28px,3.4vw,40px)] font-semibold leading-[1.1] tracking-[-0.02em]">
              Watch your first URL in a minute.
            </h2>
            <div className="flex flex-wrap gap-3">
              <Link to="/signup" className="btn btn-primary btn-lg">
                Create an account <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
              </Link>
              <Link to="/status/pulse-demo" className="btn btn-lg border-white/25 text-white hover:bg-white/10">
                Open the demo status page
              </Link>
            </div>
          </section>
        </main>

        <footer className="flex flex-wrap items-center justify-between gap-3 px-3 py-6 text-sm text-muted md:px-8">
          <span>Pulse</span>
          <span className="flex items-center gap-4">
            <span className="mono text-xs">Built on .NET, React and PostgreSQL</span>
            <ThemeToggle />
          </span>
        </footer>
      </div>
    </div>
    </Tooltip.Provider>
  )
}
