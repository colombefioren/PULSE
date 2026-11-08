import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Logo, LogoMark } from './Logo'

describe('Logo', () => {
  it('is exposed to assistive tech as "Pulse"', () => {
    render(<Logo />)
    expect(screen.getByRole('img', { name: 'Pulse' })).toBeInTheDocument()
  })

  it('keeps the traced proportions at any height', () => {
    render(<Logo height={36} />)
    const svg = screen.getByRole('img', { name: 'Pulse' })
    expect(Number(svg.getAttribute('width')) / Number(svg.getAttribute('height'))).toBeCloseTo(1090 / 359, 2)
  })

  it('adds space between the icon and the wordmark without scaling the shapes', () => {
    const { container, rerender } = render(<Logo height={22} />)
    const plain = container.querySelector('svg')!
    const plainWidth = Number(plain.getAttribute('width'))

    rerender(<Logo height={22} iconGap={10} />)
    const spaced = container.querySelector('svg')!

    expect(Number(spaced.getAttribute('width'))).toBeCloseTo(plainWidth + 10, 1)
    expect(spaced.getAttribute('height')).toBe('22')
    expect(spaced.querySelector('g')).toHaveAttribute('transform', expect.stringMatching(/^translate\(/))
  })

  it('is hidden from assistive tech when it sits next to the same text', () => {
    const { container } = render(<Logo label="" />)
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })
})

describe('LogoMark', () => {
  it('only animates the heartbeat line when asked to', () => {
    const { container, rerender } = render(<LogoMark />)
    expect(container.querySelector('.logo-pulse-draw')).toBeNull()

    rerender(<LogoMark animate />)
    expect(container.querySelectorAll('path')).toHaveLength(2)
    expect(container.querySelector('.logo-pulse-draw')).not.toBeNull()
  })
})
