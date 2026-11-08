import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router'

/**
 * Shown after a valid sign-in or sign-up while accounts are not live yet (milestone M1).
 * It says so plainly and points at the demo workspace instead of pretending to sign in.
 */
export function AccountsNotice({ action }: { action: 'sign in' | 'sign up' }) {
  return (
    <div role="status" className="rounded-2xl bg-surface p-5">
      <p className="text-base font-semibold">Accounts are not open yet</p>
      <p className="mt-1 text-base text-muted">
        Your details look fine, but nobody can {action} until accounts ship in the next release. The whole product runs
        on a demo workspace in the meantime.
      </p>
      <Link to="/dashboard" className="btn btn-primary btn-lg mt-4 w-full">
        Open the demo workspace <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
      </Link>
    </div>
  )
}
