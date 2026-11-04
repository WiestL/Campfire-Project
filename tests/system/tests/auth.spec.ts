import { test, expect } from '@playwright/test'

// Workflow: a new visitor registers, lands on their role's dashboard, and can
// log back in later. Covers the auth slice of REQ-001/002 end to end through
// the real UI, API, and Postgres, not a mocked fetch.

test('a Member can register, land on the Member Dashboard, and see it again after logging back in', async ({ page }) => {
  const email = `system-member-${Date.now()}@example.com`

  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('System Test Member')
  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.click('button[type="submit"]')

  await expect(page).toHaveURL('/')
  await expect(page.locator('text=Member Dashboard')).toBeVisible()

  await page.click('text=Log out')
  await expect(page).toHaveURL(/\/login/)

  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.click('button[type="submit"]')

  await expect(page).toHaveURL('/')
  await expect(page.locator('text=Member Dashboard')).toBeVisible()
})

test('a Creator registering lands on the Creator Dashboard, not the Member one', async ({ page }) => {
  const email = `system-creator-${Date.now()}@example.com`

  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('System Test Creator')
  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.locator('label:has-text("I am a") select').selectOption('Creator')
  await page.click('button[type="submit"]')

  await expect(page).toHaveURL('/')
  await expect(page.locator('text=Creator Dashboard')).toBeVisible()
})

test('logging in with a wrong password is rejected, not silently accepted', async ({ page }) => {
  const email = `system-wrongpass-${Date.now()}@example.com`

  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Wrong Password Test')
  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')
  await page.click('text=Log out')

  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('definitely-the-wrong-password')
  await page.click('button[type="submit"]')

  await expect(page).toHaveURL(/\/login/)
  await expect(page.locator('.auth-error')).toBeVisible()
})

test('a visitor cannot reach the Member Dashboard without logging in first', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/\/login/)
})
