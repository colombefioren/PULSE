import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '@/router'
import { ThemeProvider } from '@/theme/ThemeProvider'

/** Renders the whole app at a path, with the same providers as production and no query retries. */
export function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchInterval: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(
    <ThemeProvider>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  )
  return { router, client, ...view }
}
