import { useEffect, useMemo, useRef, useState, useSyncExternalStore, type ReactNode } from 'react'
import { Sparkline } from '@/components/charts/Sparkline'
import type { Monitor } from '@/data/types'
import { formatMs } from '@/lib/stats'
import {
  CARD_COUNT,
  PERSPECTIVE,
  SECTION_COUNT,
  SECTION_LABELS,
  cardPose,
  formatPercent,
  sectionIndex,
  sectionOpacity,
  stageProgress,
} from '@/lib/tunnel'

const REDUCED_QUERY = '(prefers-reduced-motion: reduce)'

function useReducedMotion(): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const query = matchMedia(REDUCED_QUERY)
      query.addEventListener('change', onChange)
      return () => query.removeEventListener('change', onChange)
    },
    () => matchMedia(REDUCED_QUERY).matches,
    () => false,
  )
}


type Variant = 'dark' | 'cream' | 'alert'

function variantFor(index: number): Variant {
  if (index % 7 === 3) return 'alert'
  if (index % 5 === 0) return 'cream'
  return 'dark'
}

const SKIN: Record<Variant, string> = {
  dark: 'bg-[#1d1223] text-[#f6ecf2] ring-1 ring-white/10',
  cream: 'bg-[#f6f0e7] text-[#2a1a2e]',
  alert: 'bg-brand text-brand-ink',
}

function TunnelCard({ monitor, variant }: { monitor: Monitor; variant: Variant }) {
  const alert = variant === 'alert'
  return (
    <div className={`h-[132px] w-[200px] rounded-3xl p-4 sm:h-[148px] sm:w-[248px] sm:p-5 ${SKIN[variant]}`}>
      <div className="flex items-center justify-between gap-2">
        <span className="truncate text-sm font-semibold">{alert ? 'Incident opened' : monitor.name}</span>
        <span className="status-dot shrink-0" data-status={alert ? 'down' : monitor.status} aria-hidden />
      </div>
      {alert ? (
        <p className="mt-3 text-xs leading-snug">Three failed checks in a row. Team alerted by email and Slack.</p>
      ) : (
        <>
          <div className="mt-3">
            <Sparkline
              points={monitor.spark}
              width={200}
              height={40}
              color={variant === 'dark' ? 'var(--brand)' : '#2a1a2e'}
              label=""
            />
          </div>
          <p className="mono mt-2 text-xs opacity-70">{formatMs(monitor.lastResponseMs)}</p>
        </>
      )}
    </div>
  )
}

interface TunnelProps {
  monitors: Monitor[]
  /** One node per section, in order. */
  sections: ReactNode[]
}

/**
 * The scrollable stage: a tunnel of monitor cards that fly toward the viewer while text sections
 * cross-fade, a scroll ruler on the right edge and a section index. Under prefers-reduced-motion it
 * becomes a plain stack of sections with no scroll-driven movement.
 */
export function Tunnel({ monitors, sections }: TunnelProps) {
  const reduced = useReducedMotion()
  const stageRef = useRef<HTMLDivElement>(null)
  const cardRefs = useRef<(HTMLDivElement | null)[]>([])
  const sectionRefs = useRef<(HTMLDivElement | null)[]>([])
  const markerRef = useRef<HTMLDivElement>(null)
  const labelRef = useRef<HTMLSpanElement>(null)
  const [active, setActive] = useState(0)

  const cards = useMemo(
    () => Array.from({ length: CARD_COUNT }, (_, i) => ({ monitor: monitors[i % monitors.length], variant: variantFor(i) })),
    [monitors],
  )

  useEffect(() => {
    if (reduced) return
    let frame = 0

    const apply = () => {
      frame = 0
      const stage = stageRef.current
      if (!stage) return
      const rect = stage.getBoundingClientRect()
      const progress = stageProgress(rect.top, rect.height, innerHeight)
      const radius = Math.min(innerWidth, innerHeight * 1.5) * 0.4

      cardRefs.current.forEach((element, i) => {
        if (!element) return
        const pose = cardPose(i, progress, radius)
        element.style.transform = `translate3d(${pose.x}px, ${pose.y}px, ${pose.z}px) translate(-50%, -50%) rotate(${pose.rotate}deg)`
        element.style.opacity = String(pose.opacity)
      })

      sectionRefs.current.forEach((element, i) => {
        if (!element) return
        const opacity = sectionOpacity(i, progress)
        element.style.opacity = String(opacity)
        element.style.transform = `translateY(${(1 - opacity) * 28}px)`
        element.style.pointerEvents = opacity > 0.6 ? 'auto' : 'none'
        element.toggleAttribute('inert', opacity <= 0.6)
      })

      if (markerRef.current) markerRef.current.style.top = `${progress * 100}%`
      if (labelRef.current) labelRef.current.textContent = formatPercent(progress)
      setActive(sectionIndex(progress))
    }

    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(apply)
    }

    apply()
    addEventListener('scroll', schedule, { passive: true })
    addEventListener('resize', schedule)
    return () => {
      cancelAnimationFrame(frame)
      removeEventListener('scroll', schedule)
      removeEventListener('resize', schedule)
    }
  }, [reduced, cards])

  const jumpTo = (index: number) => {
    const stage = stageRef.current
    if (!stage) return
    const top = stage.getBoundingClientRect().top + scrollY
    const travel = stage.offsetHeight - innerHeight
    scrollTo({ top: top + (index / SECTION_COUNT + 0.05) * travel, behavior: 'smooth' })
  }

  if (reduced) {
    return (
      <div>
        {sections.map((section, i) => (
          <div key={SECTION_LABELS[i]} className="flex min-h-dvh items-center px-6 py-24 md:px-16">
            {section}
          </div>
        ))}
      </div>
    )
  }

  return (
    <div ref={stageRef} className="relative" style={{ height: `${SECTION_COUNT * 110}vh` }}>
      <div className="sticky top-0 h-dvh overflow-hidden">
        <div aria-hidden className="absolute inset-0" style={{ perspective: PERSPECTIVE, perspectiveOrigin: '50% 50%' }}>
          <div className="absolute left-[66%] top-1/2 size-0 max-md:left-1/2" style={{ transformStyle: 'preserve-3d' }}>
            {cards.map(({ monitor, variant }, i) =>
              monitor ? (
                <div
                  key={i}
                  ref={(el) => {
                    cardRefs.current[i] = el
                  }}
                  className="absolute left-0 top-0 will-change-transform"
                  style={{ opacity: 0 }}
                >
                  <TunnelCard monitor={monitor} variant={variant} />
                </div>
              ) : null,
            )}
          </div>
        </div>

        {sections.map((section, i) => (
          <div
            key={SECTION_LABELS[i]}
            ref={(el) => {
              sectionRefs.current[i] = el
            }}
            className="absolute inset-0 flex items-center px-6 md:px-16"
            style={{ opacity: i === 0 ? 1 : 0 }}
          >
            {section}
          </div>
        ))}
      </div>

      <div aria-hidden className="pointer-events-none fixed inset-y-0 right-0 z-30 hidden w-14 sm:block">
        {Array.from({ length: 11 }, (_, i) => (
          <div key={i} className="absolute right-0 flex items-center gap-2" style={{ top: `${i * 10}%` }}>
            <span className="mono text-[9px] text-white/35">{i}</span>
            <span className={`h-px bg-white/30 ${i % 5 === 0 ? 'w-5' : 'w-3'}`} />
          </div>
        ))}
        <div ref={markerRef} className="absolute right-0 flex items-center gap-2" style={{ top: 0 }}>
          <span ref={labelRef} className="mono text-[10px] font-semibold text-brand">000%</span>
          <span className="h-px w-8 bg-brand" />
        </div>
      </div>

      <nav aria-label="Sections" className="fixed bottom-8 right-24 z-30 hidden text-right md:block">
        <ul className="flex flex-col gap-1">
          {SECTION_LABELS.map((label, i) => (
            <li key={label}>
              <button
                type="button"
                onClick={() => jumpTo(i)}
                aria-current={active === i ? 'true' : undefined}
                className={`text-sm font-extrabold uppercase tracking-tight transition-colors ${
                  active === i ? 'text-white' : 'text-white/35 hover:text-white/70'
                }`}
              >
                {label}
              </button>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
