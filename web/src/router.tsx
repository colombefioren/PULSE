import type { ComponentType } from 'react'
import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { AppShell } from '@/layout/AppShell'
import type { RouteHandle } from '@/layout/Topbar'

/** Every page is loaded on demand so the initial bundle stays small. */
function page(load: () => Promise<{ default: ComponentType }>, crumb: string): RouteObject {
  return {
    lazy: async () => ({ Component: (await load()).default }),
    handle: { crumb } satisfies RouteHandle,
  }
}

export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppShell,
    children: [
      { index: true, element: <Navigate to="/monitors" replace /> },
      { path: 'monitors', ...page(() => import('@/pages/MonitorsPage'), 'Monitors') },
      { path: 'incidents', ...page(() => import('@/pages/IncidentsPage'), 'Incidents') },
      { path: 'status-pages', ...page(() => import('@/pages/StatusPagesPage'), 'Status pages') },
      { path: 'alerts', ...page(() => import('@/pages/AlertsPage'), 'Alerts') },
      { path: 'settings', ...page(() => import('@/pages/SettingsPage'), 'Settings') },
      { path: '*', ...page(() => import('@/pages/NotFoundPage'), 'Not found') },
    ],
  },
]

export const router = createBrowserRouter(routes)
