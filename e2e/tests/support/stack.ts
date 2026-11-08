import { expect, type Page } from '@playwright/test'

/** The address the app (running in Docker) uses to reach the target site, and the one the test uses to switch it. */
export const TARGET_FOR_PULSE = process.env.TARGET_FOR_PULSE ?? 'http://target:4010/health'
export const TARGET_CONTROL = process.env.TARGET_CONTROL ?? 'http://localhost:4010'
export const MAIL_API = process.env.MAIL_API ?? 'http://localhost:8025'

export const PASSWORD = 'correct-horse-battery-staple'

export async function setTarget(state: 'up' | 'down') {
  const response = await fetch(`${TARGET_CONTROL}/control/${state}`, { method: 'POST' })
  expect(response.ok, `could not switch the target ${state}`).toBe(true)
}

interface MailMessage {
  ID: string
  To: { Address: string }[]
}

/** Finds the link in the newest mail to `address`, waiting for it to arrive. Uses Mailpit's HTTP API. */
export async function linkInMail(address: string, contains: string): Promise<string> {
  let link = ''
  await expect
    .poll(
      async () => {
        const list = (await (await fetch(`${MAIL_API}/api/v1/messages`)).json()) as { messages?: MailMessage[] }
        const mine = (list.messages ?? []).find((message) => message.To.some((to) => to.Address.toLowerCase() === address.toLowerCase()))
        if (!mine) return ''
        const body = (await (await fetch(`${MAIL_API}/api/v1/message/${mine.ID}`)).json()) as { Text?: string; HTML?: string }
        const text = `${body.Text ?? ''} ${body.HTML ?? ''}`
        const found = text.match(new RegExp(`https?://[^\\s"'<>]*${contains}[^\\s"'<>]*`))
        link = found ? found[0].replace(/&amp;/g, '&') : ''
        return link
      },
      { message: `waiting for a mail to ${address}`, timeout: 30_000, intervals: [500, 1000, 2000] },
    )
    .not.toBe('')
  return link
}

export interface Account {
  name: string
  team: string
  email: string
}

export function newAccount(): Account {
  const id = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`
  return { name: 'E2E Tester', team: `E2E Team ${id}`, email: `e2e-${id}@example.test` }
}

/** Signs up in the browser, follows the confirmation link from the caught mail and signs in. */
export async function signUp(page: Page, account: Account) {
  await page.goto('/signup')
  await page.getByLabel('Your name').fill(account.name)
  await page.getByLabel('Team name').fill(account.team)
  await page.getByLabel('Work email').fill(account.email)
  await page.getByLabel('Password').fill(PASSWORD)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible()

  await page.goto(await linkInMail(account.email, 'verify-email'))
  await expect(page.getByRole('heading', { name: 'Email confirmed' })).toBeVisible()

  await signIn(page, account)
}

export async function signIn(page: Page, account: Account) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(account.email)
  await page.getByLabel('Password').fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}
