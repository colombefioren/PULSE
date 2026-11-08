import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router'
import { Logo } from '@/components/Logo'
import { StatusPageView } from '@/components/StatusPageView'
import { Tunnel } from '@/components/landing/Tunnel'
import { useMonitors, useStatusPage } from '@/data/queries'

const DISPLAY = 'font-extrabold uppercase tracking-[-0.045em] leading-[0.92]'

function Eyebrow({ index, children }: { index?: string; children: string }) {
  return (
    <p className="mono mb-5 flex items-center gap-3 text-xs uppercase tracking-[0.18em] text-white/55">
      {index && <span className="text-brand">{index}</span>}
      {children}
    </p>
  )
}

function Section({ index, eyebrow, title, body }: { index: string; eyebrow: string; title: string; body: string }) {
  return (
    <div className="max-w-5xl">
      <Eyebrow index={index}>{eyebrow}</Eyebrow>
      <h2 className={`${DISPLAY} text-[clamp(40px,7.2vw,112px)] text-white`}>{title}</h2>
      <p className="mt-6 max-w-xl text-md text-white/65">{body}</p>
    </div>
  )
}

export default function LandingPage() {
  const monitors = useMonitors()
  const statusPage = useStatusPage('pulse-demo')

  const sections = [
    <div key="hero" className="max-w-6xl">
      <Eyebrow>Uptime monitoring and public status pages</Eyebrow>
      <h1 className={`${DISPLAY} text-[clamp(64px,13.5vw,216px)] text-white`}>
        Know
        <br />
        first<span className="text-brand">.</span>
      </h1>
      <p className="mt-6 max-w-lg text-md text-white/65">
        Pulse checks your sites and APIs every 30 seconds, opens an incident when they stop answering, and keeps a public
        page honest while you fix it.
      </p>
      <div className="mt-8 flex flex-wrap gap-3">
        <Link to="/dashboard" className="btn btn-primary btn-lg">
          Try the demo <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
        </Link>
        <Link to="/signup" className="btn btn-lg border-white/25 text-white hover:bg-white/10">
          Sign up
        </Link>
      </div>
    </div>,
    <Section
      key="detect"
      index="01"
      eyebrow="Detect"
      title="Every URL, every 30 seconds"
      body="A worker pool checks each monitor on its own clock, with jitter so checks never stampede, and never checks the same monitor twice."
    />,
    <Section
      key="decide"
      index="02"
      eyebrow="Decide"
      title="Three misses open an incident"
      body="You pick how many failures open an incident and how many successes close it. No flapping, no 3 a.m. noise from one slow response."
    />,
    <Section
      key="alert"
      index="03"
      eyebrow="Alert"
      title="One message, delivered once"
      body="Email, Slack, Discord or a signed webhook. Every state change is written to an outbox and retried with backoff, so it is sent exactly when it should be."
    />,
    <Section
      key="prove"
      index="04"
      eyebrow="Prove"
      title="A status page that tells the truth"
      body="Ninety days of daily uptime, live incident updates and a response-time graph, on a page that loads fast without a sign-in."
    />,
  ]

  return (
    <div className="bg-[#0b070d] text-white">
      <header className="fixed inset-x-0 top-0 z-30 flex items-center justify-between px-6 py-5 md:px-16">
        <Link to="/" aria-label="Pulse home">
          <Logo height={28} iconGap={12} />
        </Link>
        <nav aria-label="Account" className="flex items-center gap-2 md:mr-10">
          <Link to="/login" className="btn btn-ghost text-white hover:bg-white/10">
            Sign in
          </Link>
          <Link to="/dashboard" className="btn btn-primary">
            Try the demo
          </Link>
        </nav>
      </header>

      <main>
        {monitors.data ? (
          <Tunnel monitors={monitors.data} sections={sections} />
        ) : (
          <div className="flex min-h-dvh items-center px-6 md:px-16">{sections[0]}</div>
        )}

        <section aria-labelledby="live-heading" className="px-4 py-24 md:px-16 md:py-36">
          <Eyebrow index="05">Live preview</Eyebrow>
          <h2 id="live-heading" className={`${DISPLAY} max-w-5xl text-[clamp(40px,7.2vw,112px)]`}>
            This is what your customers see
          </h2>
          <p className="mt-6 max-w-xl text-md text-white/65">
            A real status page, running on the demo workspace right now. Hover the bars. Watch the auth service.
          </p>
          <div className="mt-12 rounded-shell bg-bg p-4 text-fg md:p-10">
            {statusPage.data ? (
              <StatusPageView page={statusPage.data} embedded />
            ) : (
              <div className="skeleton h-[480px]" aria-label="Loading status page" />
            )}
          </div>
        </section>

        <section className="px-6 pb-28 pt-12 md:px-16">
          <h2 className={`${DISPLAY} text-[clamp(56px,12vw,200px)]`}>
            Start
            <br />
            watching<span className="text-brand">.</span>
          </h2>
          <div className="mt-10 flex flex-wrap gap-3">
            <Link to="/signup" className="btn btn-primary btn-lg">
              Create an account <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
            </Link>
            <Link to="/status/pulse-demo" className="btn btn-lg border-white/25 text-white hover:bg-white/10">
              Open the demo status page
            </Link>
          </div>
        </section>
      </main>

      <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-white/10 px-6 py-6 text-sm text-white/55 md:px-16">
        <span>Pulse</span>
        <span className="mono text-xs">Built on .NET, React and PostgreSQL</span>
      </footer>
    </div>
  )
}
