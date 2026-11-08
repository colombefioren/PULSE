import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import { routes } from '@/router'
import { ThemeProvider } from '@/theme/ThemeProvider'
import { destinationForGoKey, isTypingTarget } from './shortcuts'

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(
    <ThemeProvider>
      <RouterProvider router={router} />
    </ThemeProvider>,
  )
  return router
}

describe('AppShell', () => {
  it('lists the five sections in the sidebar and marks the current one', async () => {
    renderAt('/monitors')

    const nav = await screen.findByRole('navigation', { name: 'Main' })
    const links = within(nav).getAllByRole('link')

    expect(links.map((link) => link.textContent)).toEqual([
      'Monitors',
      'Incidents',
      'Status pages',
      'Alerts',
      'Settings',
    ])
    expect(within(nav).getByRole('link', { name: 'Monitors' })).toHaveAttribute('aria-current', 'page')
  })

  it('shows breadcrumbs for the current page', async () => {
    renderAt('/incidents')

    const crumbs = await screen.findByRole('navigation', { name: 'Breadcrumb' })

    expect(within(crumbs).getByText('Incidents')).toHaveAttribute('aria-current', 'page')
  })

  it('redirects the root to monitors', async () => {
    const router = renderAt('/')

    expect(await screen.findByRole('heading', { name: 'Monitors' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/monitors')
  })

  it('shows a designed empty state with its one action', async () => {
    renderAt('/monitors')

    expect(await screen.findByRole('heading', { name: 'No monitors yet' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add your first monitor' })).toBeInTheDocument()
  })

  it('shows a page not found screen for unknown addresses', async () => {
    renderAt('/nope')

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to monitors' })).toBeInTheDocument()
  })

  it('collapses the sidebar to icons and remembers it', async () => {
    const user = userEvent.setup()
    renderAt('/monitors')

    await user.click(await screen.findByRole('button', { name: 'Collapse sidebar' }))

    expect(screen.getByRole('button', { name: 'Expand sidebar' })).toHaveAttribute('aria-expanded', 'false')
    expect(localStorage.getItem('pulse-sidebar-collapsed')).toBe('1')
    expect(within(screen.getByRole('navigation', { name: 'Main' })).getByRole('link', { name: 'Alerts' })).toBeInTheDocument()
  })

  it('cycles the theme from system to light to dark and stores the choice', async () => {
    const user = userEvent.setup()
    renderAt('/monitors')

    await user.click(await screen.findByRole('button', { name: /^Theme: System/ }))
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem('pulse-theme')).toBe('light')

    await user.click(screen.getByRole('button', { name: /^Theme: Light/ }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem('pulse-theme')).toBe('dark')

    await user.click(screen.getByRole('button', { name: /^Theme: Dark/ }))
    expect(localStorage.getItem('pulse-theme')).toBeNull()
  })

  it('goes to incidents with G then I and to monitors with G then M', async () => {
    const user = userEvent.setup()
    const router = renderAt('/settings')
    await screen.findByRole('heading', { name: 'Settings' })

    await user.keyboard('gi')
    expect(await screen.findByRole('heading', { name: 'No incidents' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/incidents')

    await user.keyboard('gm')
    expect(await screen.findByRole('heading', { name: 'No monitors yet' })).toBeInTheDocument()
  })

  it('ignores G shortcuts while typing in a field', async () => {
    const user = userEvent.setup()
    const router = renderAt('/settings')
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
    expect(destinationForGoKey('z')).toBeUndefined()
  })

  it('treats form fields as typing targets but not buttons', () => {
    expect(isTypingTarget(document.createElement('input'))).toBe(true)
    expect(isTypingTarget(document.createElement('textarea'))).toBe(true)
    expect(isTypingTarget(document.createElement('button'))).toBe(false)
    expect(isTypingTarget(null)).toBe(false)
  })
})
