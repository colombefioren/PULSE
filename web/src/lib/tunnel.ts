/**
 * Geometry for the landing page's scroll-driven tunnel of monitor cards.
 * Pure functions: the component feeds them scroll progress and writes the results to the DOM.
 */

export const CARD_COUNT = 16
/** Distance between consecutive cards along the tunnel, in px. */
export const CARD_GAP = 340
/** Perspective distance of the stage, in px. */
export const PERSPECTIVE = 900

const clamp = (value: number, min = 0, max = 1) => Math.min(max, Math.max(min, value))

/** Scroll progress through the stage: 0 when it reaches the top of the viewport, 1 when it leaves. */
export function stageProgress(top: number, height: number, viewport: number): number {
  const travel = height - viewport
  if (travel <= 0) return 0
  return clamp(-top / travel)
}

export interface CardPose {
  x: number
  y: number
  z: number
  rotate: number
  opacity: number
}

/** Cards sit on a golden-angle spiral and travel toward the camera as progress grows. */
export function cardPose(index: number, progress: number, radius: number): CardPose {
  const angle = index * 2.39996 + progress * 1.1
  const ring = radius * (0.78 + (index % 3) * 0.2)
  // The last card reaches the camera exactly as the stage ends, so the tunnel is never empty.
  const travel = progress * ((CARD_COUNT - 1) * CARD_GAP)
  const z = -index * CARD_GAP - 700 + travel + CARD_GAP * 2

  // Fade in from the far dark, fade out as the card passes the camera.
  const fadeIn = clamp((z + 3600) / 1500)
  const fadeOut = clamp((PERSPECTIVE * 0.3 - z) / 240)

  return {
    x: Math.cos(angle) * ring,
    y: Math.sin(angle) * ring * 0.72,
    z,
    rotate: Math.sin(angle) * 14,
    opacity: Math.min(fadeIn, fadeOut),
  }
}

export const SECTION_LABELS = ['Know first', 'Detect', 'Decide', 'Alert', 'Prove'] as const
export const SECTION_COUNT = SECTION_LABELS.length

/** Which of the five stage sections is current for a progress value. */
export function sectionIndex(progress: number): number {
  return Math.min(SECTION_COUNT - 1, Math.floor(progress * SECTION_COUNT))
}

/**
 * Opacity of one text section. Each owns a fifth of the scroll; it fades in over the start and out
 * over the end. The first section is visible at rest and the last stays until the stage ends.
 */
export function sectionOpacity(index: number, progress: number): number {
  const width = 1 / SECTION_COUNT
  const u = (progress - index * width) / width
  const edge = 0.2
  const fadeIn = index === 0 ? 1 : clamp(u / edge)
  const fadeOut = index === SECTION_COUNT - 1 ? 1 : clamp((1 - u) / edge)
  return u < 0 || u > 1 ? 0 : Math.min(fadeIn, fadeOut)
}

export function formatPercent(progress: number): string {
  return `${String(Math.round(progress * 100)).padStart(3, '0')}%`
}
