import { expect, test } from '@playwright/test'
import { newAccount, setTarget, signIn, signUp, TARGET_FOR_PULSE } from './support/stack'

// The two journeys the product exists for, against the real stack:
//   1. sign up, add a monitor, watch it go Up
//   2. the site goes down, an incident opens, and the public status page says so; then it recovers

test.describe.configure({ mode: 'serial' })

const account = newAccount()
const monitorName = `E2E site ${Date.now().toString(36)}`
const statusTitle = `E2E status ${Date.now().toString(36)}`
let statusSlug = ''

test.beforeAll(async () => {
  await setTarget('up')
})

test.afterAll(async () => {
  await setTarget('up')
})

test('sign up, add a monitor and see it go Up', async ({ page }) => {
  await signUp(page, account)

  await page.goto('/monitors')
  await page.getByRole('button', { name: 'Add monitor' }).first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Name').fill(monitorName)
  await dialog.getByLabel('URL').fill(TARGET_FOR_PULSE)
  await dialog.getByLabel('Check', { exact: true }).selectOption('30')
  // Open the advanced section and call the monitor down after one failed check, so the test does not wait minutes.
  await dialog.getByText('Advanced').click()
  await dialog.getByLabel('Down after (failed checks)').fill('1')
  await dialog.getByLabel('Up after (passed checks)').fill('1')
  await dialog.getByRole('button', { name: 'Add monitor' }).click()

  // The new monitor is in the list; open it.
  await page.getByRole('link', { name: monitorName }).click()
  await expect(page).toHaveURL(/\/monitors\/[0-9a-f-]{36}$/)
  await expect(page.getByRole('heading', { level: 1, name: monitorName })).toBeVisible()
  await expect(page.getByText('Up', { exact: true }).first()).toBeVisible({ timeout: 90_000 })
})

test('a public status page is ready before anything goes wrong', async ({ page }) => {
  await signIn(page, account)

  await page.goto('/status-pages')
  await page.getByRole('button', { name: /Create status page/ }).first().click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Title').fill(statusTitle)
  statusSlug = await dialog.getByLabel('Address').inputValue()
  expect(statusSlug).toMatch(/^e2e-status-/)
  await dialog.getByRole('button', { name: 'Create page' }).click()
  await expect(page.getByRole('heading', { name: statusTitle })).toBeVisible()

  await page.getByRole('button', { name: /Add component/ }).first().click()
  const component = page.getByRole('dialog')
  await component.getByLabel('Name').fill('Website')
  await component.getByRole('checkbox', { name: monitorName }).check()
  await component.getByRole('button', { name: 'Add component' }).click()
  await expect(page.getByText('Website').first()).toBeVisible()
})

test('an outage opens an incident that a visitor sees on the status page, and recovery closes it', async ({ page, browser }) => {
  await signIn(page, account)

  const visitor = await (await browser.newContext()).newPage()
  await visitor.goto(`/status/${statusSlug}`)
  await expect(visitor.getByRole('heading', { name: 'All systems operational' })).toBeVisible()

  await setTarget('down')

  // The owner's incident list opens a "is down" incident without a reload once the next check fails.
  await page.goto('/incidents')
  await expect(page.getByRole('link', { name: `${monitorName} is down` })).toBeVisible({ timeout: 120_000 })

  // The visitor sees the outage, with the monitor's details hidden behind a plain description.
  await expect(async () => {
    await visitor.reload()
    await expect(visitor.getByRole('heading', { name: 'All systems operational' })).toHaveCount(0)
    await expect(visitor.getByText(/Issue affecting Website/).first()).toBeVisible({ timeout: 3_000 })
  }).toPass({ timeout: 60_000, intervals: [3_000] })
  await expect(visitor.getByText(TARGET_FOR_PULSE)).toHaveCount(0)
  await expect(visitor.getByText(monitorName)).toHaveCount(0)

  await setTarget('up')

  await expect(async () => {
    await visitor.reload()
    await expect(visitor.getByRole('heading', { name: 'All systems operational' })).toBeVisible({ timeout: 3_000 })
  }).toPass({ timeout: 120_000, intervals: [3_000] })

  await page.goto('/incidents')
  await expect(page.getByText('Resolved').first()).toBeVisible()
  await visitor.context().close()
})

test('the uptime badge is off until switched on, then shows the figure', async ({ page, request }) => {
  await signIn(page, account)

  await page.goto('/monitors')
  await page.getByRole('link', { name: monitorName }).click()
  // The route changes without a page load, so wait for it before reading the id from the address.
  await expect(page).toHaveURL(/\/monitors\/[0-9a-f-]{36}$/)
  const id = page.url().split('/').pop()!
  expect((await request.get(`/badge/${id}.svg`)).status()).toBe(404)

  await page.getByRole('button', { name: 'Edit' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByText('Advanced').click()
  await dialog.getByRole('checkbox', { name: 'Public uptime badge' }).check()
  await dialog.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByRole('heading', { name: 'Uptime badge' })).toBeVisible()

  // An unavailable badge is remembered for a few seconds, so allow for that.
  await expect.poll(async () => (await request.get(`/badge/${id}.svg`)).status(), { timeout: 30_000, intervals: [2_000] }).toBe(200)
  const badge = await request.get(`/badge/${id}.svg`)
  expect(badge.headers()['content-type']).toContain('image/svg+xml')
  expect(await badge.text()).toMatch(/\d+(\.\d+)?%/)
})
