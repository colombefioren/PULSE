import { Navigate, Outlet, useLocation } from 'react-router'
import { useSession } from '@/session/useSession'

/** Sends anyone who is neither signed in nor in the demo to the sign-in page, and remembers where they were going. */
export function RequireSession() {
  const session = useSession()
  const location = useLocation()

  if (session.status === 'loading') {
    return (
      <div className="grid min-h-dvh place-items-center bg-canvas" role="status" aria-label="Loading">
        <div className="skeleton h-10 w-40" aria-hidden />
      </div>
    )
  }

  if (session.status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
  }

  return <Outlet />
}
