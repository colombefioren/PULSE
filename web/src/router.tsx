import type { ComponentType } from 'react'
import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { AppShell } from '@/layout/AppShell'
import { AuthLayout } from '@/layout/AuthLayout'
import type { RouteHandle } from '@/layout/Topbar'

/** Every page is loaded on demand so the initial bundle stays small. */
function page(load: () => Promise<{ default: ComponentType }>, crumb?: string): RouteObject {
  return {
    lazy: async () => ({ Component: (await load()).default }),
    handle: { crumb } satisfies RouteHandle,
  }
}

export const routes: RouteObject[] = [
  { path: '/', ...page(() => import('@/pages/LandingPage')) },
  {
    Component: AuthLayout,
    children: [
      { path: 'login', ...page(() => import('@/pages/auth/LoginPage')) },
      { path: 'signup', ...page(() => import('@/pages/auth/SignupPage')) },
    ],
  },
  { path: 'status/:slug', ...page(() => import('@/pages/PublicStatusPage')) },
  {
    Component: AppShell,
    children: [
      { path: 'dashboard', ...page(() => import('@/pages/DashboardPage'), 'Dashboard') },
      { path: 'monitors', ...page(() => import('@/pages/MonitorsPage'), 'Monitors') },
      { path: 'monitors/:id', ...page(() => import('@/pages/MonitorDetailPage'), 'Monitor') },
      { path: 'incidents', ...page(() => import('@/pages/IncidentsPage'), 'Incidents') },
      { path: 'status-pages', ...page(() => import('@/pages/StatusPagesPage'), 'Status pages') },
      { path: 'alerts', ...page(() => import('@/pages/AlertsPage'), 'Alerts') },
      { path: 'settings', ...page(() => import('@/pages/SettingsPage'), 'Settings') },
      { path: '*', ...page(() => import('@/pages/NotFoundPage'), 'Not found') },
    ],
  },
  { path: 'app', element: <Navigate to="/dashboard" replace /> },
]

export const router = createBrowserRouter(routes)
