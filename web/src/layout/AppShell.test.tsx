import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderAt } from '@/test/render'
import { destinationForGoKey, isTypingTarget } from './shortcuts'

describe('AppShell', () => {
  it('lists the six sections in the sidebar and marks the current one', async () => {
    renderAt('/monitors')

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    const links = within(nav).getAllByRole('link')

    expect(links.map((link) => link.textContent)).toEqual([
      'Dashboard',
      'Monitors',
      'Incidents',
      'Status pages',
      'Alerts',
      'Settings',
    ])
    expect(within(nav).getByRole('link', { name: 'Monitors' })).toHaveAttribute('aria-current', 'page')
  })

  it('styles only the current sidebar link as selected, with its layout classes intact', async () => {
    renderAt('/alerts')

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    const alerts = within(nav).getByRole('link', { name: 'Alerts' })

    expect(alerts).toHaveClass('flex', 'items-center', 'bg-bg')
    expect(within(nav).getByRole('link', { name: 'Monitors' })).not.toHaveClass('bg-bg')
    expect(within(nav).getByRole('link', { name: 'Monitors' })).toHaveClass('flex', 'items-center')
  })

  it('shows the workspace and who is signed in', async () => {
    renderAt('/alerts')

    expect(await screen.findByText('Pulse Demo')).toBeInTheDocument()
    expect(screen.getByText(/Colombe · Owner/)).toBeInTheDocument()
  })

  it('shows breadcrumbs for the current page', async () => {
    renderAt('/incidents')

    const crumbs = await screen.findByRole('navigation', { name: 'Breadcrumb' })

    expect(within(crumbs).getByText('Incidents')).toHaveAttribute('aria-current', 'page')
  })

  it('sends /app to the dashboard', async () => {
    const { router } = renderAt('/app')

    expect(await screen.findByRole('heading', { level: 1, name: /Colombe/ })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/dashboard')
  })

  it('shows a page not found screen for unknown addresses inside the app', async () => {
    renderAt('/nope')

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to monitors' })).toBeInTheDocument()
  })

  it('collapses the sidebar to icons and remembers it', async () => {
    const user = userEvent.setup()
    renderAt('/alerts')

    await user.click(await screen.findByRole('button', { name: 'Collapse sidebar' }))

    expect(screen.getByRole('button', { name: 'Expand sidebar' })).toHaveAttribute('aria-expanded', 'false')
    expect(localStorage.getItem('pulse-sidebar-collapsed')).toBe('1')
    expect(within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name: 'Alerts' })).toBeInTheDocument()
  })

  it('cycles the theme from system to light to dark and stores the choice', async () => {
    const user = userEvent.setup()
    renderAt('/alerts')

    await user.click(await screen.findByRole('button', { name: /^Theme: System/ }))
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('pulse-theme')).toBe('light')

    await user.click(screen.getByRole('button', { name: /^Theme: Light/ }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('pulse-theme')).toBe('dark')

    await user.click(screen.getByRole('button', { name: /^Theme: Dark/ }))
    expect(localStorage.getItem('pulse-theme')).toBeNull()
  })

  it('goes to incidents with G then I, monitors with G then M and the dashboard with G then D', async () => {
    const user = userEvent.setup()
    const { router } = renderAt('/settings')
    await screen.findByRole('heading', { name: 'Settings' })

    await user.keyboard('gi')
    await waitFor(() => expect(router.state.location.pathname).toBe('/incidents'))

    await user.keyboard('gm')
    await waitFor(() => expect(router.state.location.pathname).toBe('/monitors'))

    await user.keyboard('gd')
    await waitFor(() => expect(router.state.location.pathname).toBe('/dashboard'))
  })

  it('ignores G shortcuts while typing in a field', async () => {
    const user = userEvent.setup()
    const { router } = renderAt('/settings')
    await screen.findByRole('heading', { name: 'Settings' })

    const field = document.createElement('input')
    document.body.append(field)
    field.focus()
    await user.keyboard('gi')
    field.remove()

    expect(router.state.location.pathname).toBe('/settings')
  })
})

describe('go shortcuts', () => {
  it('maps the second key to a section and ignores unknown keys', () => {
    expect(destinationForGoKey('m')).toBe('/monitors')
    expect(destinationForGoKey('I')).toBe('/incidents')
    expect(destinationForGoKey('d')).toBe('/dashboard')
    expect(destinationForGoKey('z')).toBeUndefined()
  })

  it('treats form fields as typing targets but not buttons', () => {
    expect(isTypingTarget(document.createElement('input'))).toBe(true)
    expect(isTypingTarget(document.createElement('textarea'))).toBe(true)
    expect(isTypingTarget(document.createElement('button'))).toBe(false)
    expect(isTypingTarget(null)).toBe(false)
  })
})
