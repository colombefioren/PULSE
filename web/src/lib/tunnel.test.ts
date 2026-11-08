import { describe, expect, it } from 'vitest'
import { CARD_COUNT, cardPose, formatPercent, sectionIndex, sectionOpacity, stageProgress } from './tunnel'

describe('stageProgress', () => {
  it('is 0 at the top of the stage and 1 once it has scrolled through', () => {
    expect(stageProgress(0, 4000, 1000)).toBe(0)
    expect(stageProgress(-1500, 4000, 1000)).toBe(0.5)
    expect(stageProgress(-3000, 4000, 1000)).toBe(1)
  })

  it('stays inside 0 and 1 when scrolled above or past the stage', () => {
    expect(stageProgress(400, 4000, 1000)).toBe(0)
    expect(stageProgress(-9000, 4000, 1000)).toBe(1)
  })

  it('does not divide by zero when the stage is no taller than the viewport', () => {
    expect(stageProgress(0, 800, 1000)).toBe(0)
  })
})

describe('cardPose', () => {
  it('moves every card toward the camera as progress grows', () => {
    for (let i = 0; i < CARD_COUNT; i += 1) {
      expect(cardPose(i, 0.6, 300).z).toBeGreaterThan(cardPose(i, 0.2, 300).z)
    }
  })

  it('keeps the farthest cards invisible and passed cards faded out', () => {
    expect(cardPose(CARD_COUNT - 1, 0, 300).opacity).toBe(0)
    expect(cardPose(0, 1, 300).opacity).toBe(0)
  })

  it('shows some cards at every point of the scroll', () => {
    for (const p of [0, 0.25, 0.5, 0.75, 1]) {
      const visible = Array.from({ length: CARD_COUNT }, (_, i) => cardPose(i, p, 300).opacity).filter((o) => o > 0.05)
      expect(visible.length, `progress ${p}`).toBeGreaterThan(0)
    }
  })

  it('stays within the radius it was given', () => {
    const pose = cardPose(5, 0.4, 300)
    expect(Math.hypot(pose.x, pose.y / 0.72)).toBeLessThanOrEqual(300 * 1.2 + 0.001)
  })
})

describe('sections', () => {
  it('maps progress to one of five sections', () => {
    expect(sectionIndex(0)).toBe(0)
    expect(sectionIndex(0.45)).toBe(2)
    expect(sectionIndex(1)).toBe(4)
  })

  it('shows the first section at rest and the last at the end', () => {
    expect(sectionOpacity(0, 0)).toBe(1)
    expect(sectionOpacity(4, 1)).toBe(1)
  })

  it('shows only the current section in the middle of its window', () => {
    const p = 0.5
    expect(sectionOpacity(2, p)).toBe(1)
    expect(sectionOpacity(1, p)).toBe(0)
    expect(sectionOpacity(3, p)).toBe(0)
  })

  it('cross-fades near the boundary between two sections', () => {
    const p = 0.399
    expect(sectionOpacity(1, p)).toBeGreaterThan(0)
    expect(sectionOpacity(1, p)).toBeLessThan(1)
  })
})

describe('formatPercent', () => {
  it('pads to three digits like a counter', () => {
    expect(formatPercent(0)).toBe('000%')
    expect(formatPercent(0.04)).toBe('004%')
    expect(formatPercent(1)).toBe('100%')
  })
})
