import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useNavigate } from 'react-router'
import { api, resetCsrf } from '@/api/client'
import { fetchMe, ME_KEY } from '@/api/hooks'
import { enterDemo, leaveDemo } from '@/session/demoMode'

/**
 * "Try the demo" lands here. Where the server has a shared demo account (real checks of public websites, read-only),
 * the visitor is signed in to it. Anywhere else, or if that fails, the built-in sample workspace opens instead, which
 * needs no server at all. Someone who is already signed in keeps their own session and sees the sample workspace.
 */
export default function DemoEntry() {
  const navigate = useNavigate()
  const client = useQueryClient()

  useEffect(() => {
    let cancelled = false

    const open = async () => {
      let shared = false
      try {
        const current = await fetchMe()
        if (!current) {
          await api.post('/api/demo/session')
          resetCsrf()
          client.clear()
          leaveDemo()
          await client.fetchQuery({ queryKey: ME_KEY, queryFn: fetchMe, staleTime: 0 })
          shared = true
        }
      } catch {
        // The shared demo is not available here; the sample workspace below covers it.
      }

      if (!shared) {
        client.clear()
        enterDemo()
      }
      if (!cancelled) void navigate('/dashboard', { replace: true })
    }

    void open()
    return () => {
      cancelled = true
    }
  }, [client, navigate])

  return (
    <div className="grid min-h-dvh place-items-center bg-canvas" role="status" aria-label="Opening the demo">
      <div className="skeleton h-10 w-40" aria-hidden />
    </div>
  )
}
