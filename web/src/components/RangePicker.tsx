import type { ChartRange } from '@/data/types'

const RANGES: ChartRange[] = ['1h', '24h', '7d', '30d']

interface RangePickerProps {
  value: ChartRange
  onChange: (range: ChartRange) => void
  label?: string
}

/** Segmented control for the chart range. Arrow keys move between options. */
export function RangePicker({ value, onChange, label = 'Time range' }: RangePickerProps) {
  const move = (direction: 1 | -1) => {
    const next = RANGES[(RANGES.indexOf(value) + direction + RANGES.length) % RANGES.length]
    if (next) onChange(next)
  }

  return (
    <div
      role="radiogroup"
      aria-label={label}
      className="inline-flex rounded-full bg-surface-2 p-1"
      onKeyDown={(event) => {
        if (event.key === 'ArrowRight') move(1)
        if (event.key === 'ArrowLeft') move(-1)
      }}
    >
      {RANGES.map((range) => (
        <button
          key={range}
          type="button"
          role="radio"
          aria-checked={value === range}
          tabIndex={value === range ? 0 : -1}
          onClick={() => onChange(range)}
          className={`mono h-7 rounded-full px-3 text-xs font-medium transition-colors ${
            value === range ? 'bg-panel text-panel-fg' : 'text-muted hover:text-fg'
          }`}
        >
          {range}
        </button>
      ))}
    </div>
  )
}
