import { Link } from 'react-router'
import { PageHeader } from '@/components/PageHeader'

export default function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found" description="This address does not match anything in Pulse." />
      <Link to="/monitors" className="btn">
        Go to monitors
      </Link>
    </>
  )
}
