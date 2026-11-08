import {
  LOGO_FULL_WIDTH,
  LOGO_HEIGHT,
  LOGO_ICON_WIDTH,
  LOGO_PULSE,
  LOGO_RING,
  LOGO_WORDMARK,
} from '@/assets/logo-paths'

interface LogoProps {
  /** Rendered height in px. Width follows the logo's proportions. */
  height?: number
  className?: string
  /** Accessible name. Pass an empty string when the logo sits next to the same text. */
  label?: string
}

interface LogoMarkProps extends LogoProps {
  /** Draw the heartbeat line in once, over about 600ms. Skipped under prefers-reduced-motion. */
  animate?: boolean
}

function a11y(label: string) {
  return label ? { role: 'img' as const, 'aria-label': label } : { 'aria-hidden': true as const }
}

interface FullLogoProps extends LogoProps {
  /** Extra space in px between the ring icon and the wordmark, on top of the traced spacing. */
  iconGap?: number
}

/** Full logo: ring, heartbeat, dot and the "Pulse" wordmark, as traced from the brand PNG. */
export function Logo({ height = 24, className, label = 'Pulse', iconGap = 0 }: FullLogoProps) {
  // Convert the px gap into the logo's own units so the shapes keep their proportions.
  const extra = (iconGap * LOGO_HEIGHT) / height
  const viewWidth = LOGO_FULL_WIDTH + extra

  return (
    <svg
      viewBox={`0 0 ${viewWidth} ${LOGO_HEIGHT}`}
      width={(height * viewWidth) / LOGO_HEIGHT}
      height={height}
      fill="var(--brand)"
      fillRule="evenodd"
      className={className}
      {...a11y(label)}
    >
      <path d={LOGO_RING} />
      <path d={LOGO_PULSE} />
      <g transform={`translate(${extra} 0)`}>
        <path d={LOGO_WORDMARK} />
      </g>
    </svg>
  )
}

/** Icon only: ring, heartbeat and dot. Used for the favicon, the collapsed sidebar and status page footers. */
export function LogoMark({ height = 24, className, label = 'Pulse', animate = false }: LogoMarkProps) {
  return (
    <svg
      viewBox={`0 0 ${LOGO_ICON_WIDTH} ${LOGO_HEIGHT}`}
      width={(height * LOGO_ICON_WIDTH) / LOGO_HEIGHT}
      height={height}
      fill="var(--brand)"
      fillRule="evenodd"
      className={className}
      {...a11y(label)}
    >
      <path d={LOGO_RING} />
      <path d={LOGO_PULSE} className={animate ? 'logo-pulse-draw' : undefined} />
    </svg>
  )
}
