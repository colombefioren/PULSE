import { ExternalLink, Globe } from 'lucide-react'
import { Link } from 'react-router'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { StatusPill } from '@/components/StatusPill'
import { useStatusPage } from '@/data/queries'
import { groupComponents, overallStatus } from '@/lib/overall'

export default function StatusPagesPage() {
  const { data, isPending } = useStatusPage('pulse-demo')

  return (
    <>
      <PageHeader title="Status pages" description="Public pages that show customers how your services are doing." />

      {isPending ? (
        <div className="skeleton h-40" aria-label="Loading status pages" />
      ) : !data ? (
        <EmptyState
          icon={Globe}
          title="No status pages yet"
          description="Group monitors into components and publish them at /status/your-slug with 90 days of uptime history."
        />
      ) : (
        <article className="card rise p-5">
          <header className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="text-md">{data.title}</h2>
              <p className="mono mt-1 text-xs text-muted">/status/{data.slug}</p>
            </div>
            <Link to={`/status/${data.slug}`} className="btn">
              View public page <ExternalLink size={16} strokeWidth={1.5} aria-hidden />
            </Link>
          </header>
          <p className="mt-4 text-base font-medium">{overallStatus(data.components).headline}</p>
          <ul className="mt-3 flex flex-col">
            {groupComponents(data.components).flatMap(([group, components]) =>
              components.map((component) => (
                <li key={component.id} className="flex items-center justify-between gap-3 border-t border-border py-2.5 text-sm">
                  <span>
                    {component.name} <span className="text-subtle">· {group}</span>
                  </span>
                  <StatusPill status={component.status} />
                </li>
              )),
            )}
          </ul>
        </article>
      )}
    </>
  )
}
