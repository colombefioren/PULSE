import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useNavigate } from 'react-router'
import { enterDemo } from '@/session/demoMode'

/** "Try the demo" lands here: turn the demo on for this tab, drop anything cached, and open the dashboard. */
export default function DemoEntry() {
  const navigate = useNavigate()
  const client = useQueryClient()

  useEffect(() => {
    client.clear()
    enterDemo()
    void navigate('/dashboard', { replace: true })
  }, [client, navigate])

  return (
    <div className="grid min-h-dvh place-items-center bg-canvas" role="status" aria-label="Opening the demo">
      <div className="skeleton h-10 w-40" aria-hidden />
    </div>
  )
}
