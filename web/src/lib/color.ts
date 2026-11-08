/** Colour helpers for the accent a team picks for its status page. */

function channel(value: number): number {
  const c = value / 255
  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

/** WCAG relative luminance of a #rrggbb colour, 0 (black) to 1 (white). Null when it is not a six digit hex colour. */
export function luminance(hex: string): number | null {
  const match = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex)
  if (!match) return null
  const [r, g, b] = [match[1], match[2], match[3]].map((part) => channel(parseInt(part!, 16)))
  return 0.2126 * r! + 0.7152 * g! + 0.0722 * b!
}

export function contrast(a: string, b: string): number | null {
  const [la, lb] = [luminance(a), luminance(b)]
  if (la === null || lb === null) return null
  const [light, dark] = la > lb ? [la, lb] : [lb, la]
  return (light + 0.05) / (dark + 0.05)
}

const DARK = '#111111'
const LIGHT = '#ffffff'

/** Black or white, whichever reads better on the given background. Falls back to dark text on anything unreadable. */
export function readableOn(background: string): string {
  const onDark = contrast(background, DARK)
  const onLight = contrast(background, LIGHT)
  if (onDark === null || onLight === null) return DARK
  return onDark >= onLight ? DARK : LIGHT
}

/** True for a #rrggbb colour, the only form the server accepts. */
export function isHexColor(value: string): boolean {
  return /^#[0-9a-f]{6}$/i.test(value)
}
