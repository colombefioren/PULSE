import { useNavigate } from 'react-router'
import { DEMO_ACCOUNT_EMAIL, useLogout } from '@/api/hooks'
import { useSession } from '@/session/useSession'

/** Shown to someone signed in to the shared demo account: what they are looking at, and the way out to their own team. */
export function DemoBanner() {
  const session = useSession()
  const navigate = useNavigate()
  const logout = useLogout()

  if (session.status !== 'ready' || session.mode !== 'user' || session.user.email !== DEMO_ACCOUNT_EMAIL) return null

  return (
    <div role="region" aria-label="Demo" className="mx-4 mb-2 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-surface-2 px-4 py-3 text-sm md:mx-8">
      <p className="min-w-0">You are looking at a shared, read-only demo that checks real public websites every minute.</p>
      <button type="button" className="btn btn-primary" onClick={() => logout.mutate(undefined, { onSettled: () => void navigate('/signup') })}>
        Create your own team
      </button>
    </div>
  )
}
