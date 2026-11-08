import { Activity, Bell, Globe, LayoutDashboard, Settings, TriangleAlert, type LucideIcon } from 'lucide-react'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  /** Key pressed after G, shown in tooltips. */
  goKey?: string
}

export const NAV_ITEMS: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, goKey: 'd' },
  { to: '/monitors', label: 'Monitors', icon: Activity, goKey: 'm' },
  { to: '/incidents', label: 'Incidents', icon: TriangleAlert, goKey: 'i' },
  { to: '/status-pages', label: 'Status pages', icon: Globe },
  { to: '/alerts', label: 'Alerts', icon: Bell },
  { to: '/settings', label: 'Settings', icon: Settings },
]
