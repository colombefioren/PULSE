import { useEffect, useState } from 'react'

interface RingGaugeProps {
  /** 0 to 100. */
  value: number
  /** What the number measures, used in the accessible name. */
  label: string
  size?: number
  stroke?: number
  /** CSS color for the arc. */
  color?: string
  /** CSS color for the unfilled track. */
  track?: string
  /** Text color for the center number. */
  textColor?: string
}

/** Donut gauge. The arc sweeps in once when it first appears. */
export function RingGauge({
  value,
  label,
  size = 112,
  stroke = 12,
  color = 'var(--brand)',
  track = 'var(--plum-track)',
  textColor = 'currentColor',
}: RingGaugeProps) {
  const clamped = Math.min(100, Math.max(0, value))
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const [shown, setShown] = useState(0)

  useEffect(() => {
    const frame = requestAnimationFrame(() => setShown(clamped))
    return () => cancelAnimationFrame(frame)
  }, [clamped])

  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        role="img"
        aria-label={`${label}: ${clamped} percent`}
        className="-rotate-90"
      >
        <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke={track} strokeWidth={stroke} />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={color}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - shown / 100)}
          style={{ transition: 'stroke-dashoffset 900ms cubic-bezier(0.2, 0.7, 0.2, 1)' }}
        />
      </svg>
      <span
        aria-hidden
        className="mono absolute inset-0 flex items-center justify-center text-lg font-semibold"
        style={{ color: textColor }}
      >
        {clamped >= 99.95 ? '100' : clamped.toFixed(1)}%
      </span>
    </div>
  )
}
