import { Check, Copy } from 'lucide-react'
import { useState } from 'react'

/** The badge as it will look, and the Markdown that embeds it. The address is this site's own, so it works wherever Pulse is hosted. */
export function BadgeSnippet({ monitorId, name }: { monitorId: string; name: string }) {
  const [copied, setCopied] = useState(false)
  const path = `/badge/${monitorId}.svg`
  const markdown = `![${name} uptime](${window.location.origin}${path})`

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(markdown)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard access can be refused; the text is selectable in the box, so there is nothing else to do.
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <img src={path} alt={`${name} uptime badge`} height={20} className="h-5 self-start" />
      <div className="flex flex-wrap items-center gap-2">
        <code className="mono min-w-0 flex-1 break-all rounded-xl bg-bg px-3 py-2 text-xs" data-testid="badge-markdown">
          {markdown}
        </code>
        <button type="button" className="btn" onClick={() => void copy()}>
          {copied ? <Check size={16} strokeWidth={1.5} aria-hidden /> : <Copy size={16} strokeWidth={1.5} aria-hidden />}
          {copied ? 'Copied' : 'Copy Markdown'}
        </button>
      </div>
      <p className="text-sm text-muted">The badge shows the last 30 days and refreshes every few minutes.</p>
    </div>
  )
}
