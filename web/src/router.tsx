import type { ComponentType } from 'react'
import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { AppShell } from '@/layout/AppShell'
import { AuthLayout } from '@/layout/AuthLayout'
import { demoOnly } from '@/layout/demoOnly'
import { RequireSession } from '@/layout/RequireSession'
import type { RouteHandle } from '@/layout/Topbar'

interface PageOptions {
  crumb?: string
  /** Name of a feature whose backend is not built yet. Real accounts see a "coming" notice; the demo sees the page. */
  demoOnly?: string
}

/** Every page is loaded on demand so the initial bundle stays small. */
function page(load: () => Promise<{ default: ComponentType }>, { crumb, demoOnly: feature }: PageOptions = {}): RouteObject {
  return {
    lazy: async () => {
      const Page = (await load()).default
      return { Component: feature ? demoOnly(Page, feature) : Page }
    },
    handle: { crumb } satisfies RouteHandle,
  }
}

export const routes: RouteObject[] = [
  { path: '/', ...page(() => import('@/pages/LandingPage')) },
  { path: 'demo', ...page(() => import('@/pages/DemoEntry')) },
  {
    Component: AuthLayout,
    children: [
      { path: 'login', ...page(() => import('@/pages/auth/LoginPage')) },
      { path: 'signup', ...page(() => import('@/pages/auth/SignupPage')) },
      { path: 'verify-email', ...page(() => import('@/pages/auth/VerifyEmailPage')) },
      { path: 'forgot-password', ...page(() => import('@/pages/auth/ForgotPasswordPage')) },
      { path: 'reset-password', ...page(() => import('@/pages/auth/ResetPasswordPage')) },
      { path: 'invites/:token', ...page(() => import('@/pages/auth/InvitePage')) },
    ],
  },
  { path: 'status/:slug', ...page(() => import('@/pages/PublicStatusPage')) },
  {
    Component: RequireSession,
    children: [
      {
        Component: AppShell,
        children: [
          { path: 'dashboard', ...page(() => import('@/pages/DashboardPage'), { crumb: 'Dashboard', demoOnly: 'Dashboard' }) },
          { path: 'monitors', ...page(() => import('@/pages/MonitorsPage'), { crumb: 'Monitors', demoOnly: 'Monitors' }) },
          { path: 'monitors/:id', ...page(() => import('@/pages/MonitorDetailPage'), { crumb: 'Monitor', demoOnly: 'Monitors' }) },
          { path: 'incidents', ...page(() => import('@/pages/IncidentsPage'), { crumb: 'Incidents', demoOnly: 'Incidents' }) },
          { path: 'status-pages', ...page(() => import('@/pages/StatusPagesPage'), { crumb: 'Status pages', demoOnly: 'Status pages' }) },
          { path: 'alerts', ...page(() => import('@/pages/AlertsPage'), { crumb: 'Alerts', demoOnly: 'Alerts' }) },
          { path: 'settings', ...page(() => import('@/pages/SettingsPage'), { crumb: 'Settings' }) },
          { path: '*', ...page(() => import('@/pages/NotFoundPage'), { crumb: 'Not found' }) },
        ],
      },
    ],
  },
  { path: 'app', element: <Navigate to="/dashboard" replace /> },
]

export const router = createBrowserRouter(routes)
