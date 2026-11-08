import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import type { SeriesPoint } from '@/lib/stats'
import { demo } from './demo'
import type { ChartRange, DashboardSummary, Incident, Monitor, StatusPage } from './types'

/**
 * Every screen reads data through these hooks. They currently resolve against the demo workspace;
 * when the API ships, only the query functions below change.
 */

const LIVE_MS = 5000

const keys = {
  monitors: ['monitors'] as const,
  series: (id: string, range: ChartRange) => ['series', id, range] as const,
  incidents: ['incidents'] as const,
  summary: ['summary'] as const,
  statusPage: (slug: string) => ['status-page', slug] as const,
}

export function useMonitors() {
  return useQuery<Monitor[]>({
    queryKey: keys.monitors,
    queryFn: () => {
      demo.tickIfDue(LIVE_MS - 500)
      return [...demo.listMonitors()]
    },
    refetchInterval: LIVE_MS,
  })
}

export function useMonitor(id: string) {
  return useQuery<Monitor | null>({
    queryKey: ['monitor', id],
    queryFn: () => {
      demo.tickIfDue(LIVE_MS - 500)
      return demo.getMonitor(id) ?? null
    },
    refetchInterval: LIVE_MS,
  })
}

export function useSeries(id: string, range: ChartRange) {
  return useQuery<SeriesPoint[]>({
    queryKey: keys.series(id, range),
    queryFn: () => demo.series(id, range),
    staleTime: 60_000,
  })
}

export function useGlobalSeries(range: ChartRange) {
  return useQuery<SeriesPoint[]>({
    queryKey: ['series', 'global', range],
    queryFn: () => demo.globalSeries(range),
    staleTime: 60_000,
  })
}

export function useIncidents() {
  return useQuery<Incident[]>({
    queryKey: keys.incidents,
    queryFn: () => [...demo.listIncidents()],
    refetchInterval: LIVE_MS,
  })
}

export function useSummary() {
  return useQuery<DashboardSummary>({
    queryKey: keys.summary,
    queryFn: () => {
      demo.tickIfDue(LIVE_MS - 500)
      return demo.summary()
    },
    refetchInterval: LIVE_MS,
  })
}

export function useStatusPage(slug: string) {
  return useQuery<StatusPage | null>({
    queryKey: keys.statusPage(slug),
    queryFn: () => demo.statusPage(slug) ?? null,
    refetchInterval: LIVE_MS,
  })
}

/** Pause or resume a monitor. The row flips immediately and rolls back if the call fails. */
export function useSetMonitorPaused() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, paused }: { id: string; paused: boolean }) => demo.setPaused(id, paused),
    onMutate: async ({ id, paused }) => {
      await client.cancelQueries({ queryKey: keys.monitors })
      const previous = client.getQueryData<Monitor[]>(keys.monitors)
      client.setQueryData<Monitor[]>(keys.monitors, (list) =>
        list?.map((monitor) => (monitor.id === id ? { ...monitor, status: paused ? 'paused' : 'up' } : monitor)),
      )
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context?.previous) client.setQueryData(keys.monitors, context.previous)
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.monitors })
      void client.invalidateQueries({ queryKey: ['monitor'] })
      void client.invalidateQueries({ queryKey: keys.summary })
    },
  })
}

/** Acknowledge an incident. The list updates immediately and rolls back if the call fails. */
export function useAcknowledgeIncident() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => demo.acknowledge(id),
    onMutate: async (id) => {
      await client.cancelQueries({ queryKey: keys.incidents })
      const previous = client.getQueryData<Incident[]>(keys.incidents)
      client.setQueryData<Incident[]>(keys.incidents, (list) =>
        list?.map((incident) => (incident.id === id ? { ...incident, acknowledgedAt: Date.now() } : incident)),
      )
      return { previous }
    },
    onError: (_error, _id, context) => {
      if (context?.previous) client.setQueryData(keys.incidents, context.previous)
    },
    onSettled: () => void client.invalidateQueries({ queryKey: keys.incidents }),
  })
}

/** Re-renders every second so "12s ago" stays true without refetching. */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])
  return now
}
