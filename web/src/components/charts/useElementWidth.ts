import { useEffect, useState, type RefObject } from 'react'

/** Tracks an element's width so SVG charts can draw at the real pixel size. */
export function useElementWidth(ref: RefObject<HTMLElement | null>, fallback: number): number {
  const [width, setWidth] = useState(fallback)

  useEffect(() => {
    const element = ref.current
    if (!element) return
    setWidth(Math.round(element.getBoundingClientRect().width) || fallback)
    if (typeof ResizeObserver === 'undefined') return

    const observer = new ResizeObserver(([entry]) => {
      if (entry) setWidth(Math.round(entry.contentRect.width) || fallback)
    })
    observer.observe(element)
    return () => observer.disconnect()
  }, [ref, fallback])

  return width
}
