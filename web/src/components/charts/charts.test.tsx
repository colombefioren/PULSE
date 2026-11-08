import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { DayUptime } from '@/data/types'
import type { SeriesPoint } from '@/lib/stats'
import { StatusPill } from '../StatusPill'
import { ResponseChart } from './ResponseChart'
import { RingGauge } from './RingGauge'
import { Sparkline } from './Sparkline'
import { UptimeBars } from './UptimeBars'

const days: DayUptime[] = [
  { date: '2026-11-06', uptimePct: 100, downtimeS: 0, incidents: 0 },
  { date: '2026-11-07', uptimePct: 97.5, downtimeS: 2160, incidents: 1 },
  { date: '2026-11-08', uptimePct: null, downtimeS: 0, incidents: 0 },
]

const series: SeriesPoint[] = [
  { t: 0, ms: 100 },
  { t: 60_000, ms: 220 },
  { t: 120_000, ms: null },
  { t: 180_000, ms: 140 },
]

describe('RingGauge', () => {
  it('names the measure and clamps the value', () => {
    render(<RingGauge value={140} label="Uptime 30d" />)
    expect(screen.getByRole('img', { name: 'Uptime 30d: 100 percent' })).toBeInTheDocument()
  })

  it('shows two decimals in the nines and one below', () => {
    const { rerender } = render(<RingGauge value={99.37} label="Uptime" />)
    expect(screen.getByText('99.37%')).toBeInTheDocument()

    rerender(<RingGauge value={87.25} label="Uptime" />)
    expect(screen.getByText('87.3%')).toBeInTheDocument()
  })
})

describe('Sparkline', () => {
  it('draws one line per run of successful checks', () => {
    const { container } = render(<Sparkline points={series} label="Last 24 hours" />)
    expect(container.querySelectorAll('path')).toHaveLength(2)
  })

  it('says so when there is nothing to draw', () => {
    render(<Sparkline points={[]} label="Last 24 hours" />)
    expect(screen.getByText('No data')).toBeInTheDocument()
  })
})

describe('UptimeBars', () => {
  it('draws one bar per day and summarizes the bad ones for assistive tech', () => {
    const { container } = render(<UptimeBars days={days} subject="Public API" />)
    expect(screen.getByRole('img', { name: /Public API: 3 days of uptime\. 1 days with downtime\./ })).toBeInTheDocument()
    expect(container.querySelectorAll('[role="img"] > div')).toHaveLength(3)
  })

  it('shows the date, uptime and downtime for the hovered day', () => {
    const { container } = render(<UptimeBars days={days} subject="Public API" />)
    fireEvent.pointerEnter(container.querySelectorAll('[role="img"] > div')[1]!)

    expect(screen.getByText('7 Nov 2026')).toBeInTheDocument()
    expect(screen.getByText('97.5% uptime')).toBeInTheDocument()
    expect(screen.getByText(/36m down · 1 incident/)).toBeInTheDocument()
  })

  it('says no data for days that were not monitored', () => {
    const { container } = render(<UptimeBars days={days} subject="Public API" />)
    fireEvent.pointerEnter(container.querySelectorAll('[role="img"] > div')[2]!)
    expect(screen.getByText('No data')).toBeInTheDocument()
  })
})

describe('ResponseChart', () => {
  it('gives a text summary of the plotted series', () => {
    render(<ResponseChart series={series} range="1h" subject="Public API" />)
    expect(screen.getByText(/average 153 milliseconds.*1 failed checks out of 4/)).toBeInTheDocument()
  })

  it('reads out a failed check when hovering one', () => {
    const { container } = render(<ResponseChart series={series} range="1h" subject="Public API" />)
    const svg = container.querySelector('svg')!
    svg.getBoundingClientRect = () => ({ left: 0, top: 0, right: 720, bottom: 260, width: 720, height: 260, x: 0, y: 0, toJSON: () => ({}) })

    // x of the third point (the failed one) in a 720px wide chart with a 48px left margin.
    fireEvent.pointerMove(svg, { clientX: 48 + (2 / 3) * (720 - 48 - 12) })

    expect(screen.getByText('Check failed')).toBeInTheDocument()
  })
})

describe('StatusPill', () => {
  it('pairs the dot with a word', () => {
    render(<StatusPill status="down" />)
    expect(screen.getByText('Down')).toBeInTheDocument()
  })
})
