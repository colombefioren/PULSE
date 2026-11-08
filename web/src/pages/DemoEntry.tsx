import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
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
  // React runs effects twice in development. A second run would see the session the first one just opened.
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

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
      void navigate('/dashboard', { replace: true })
    }

    void open()
  }, [client, navigate])

  return (
    <div className="grid min-h-dvh place-items-center bg-canvas" role="status" aria-label="Opening the demo">
      <div className="skeleton h-10 w-40" aria-hidden />
    </div>
  )
}
