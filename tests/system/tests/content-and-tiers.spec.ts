import { test, expect } from '@playwright/test'

// Workflow: a Creator defines a membership tier, then publishes a post, and
// that post shows up in the live feed. Covers REQ-003 (tiers) and REQ-004
// (publish content) end to end.

test('a Creator can create a tier and publish a post, and both appear where members expect them', async ({ page }) => {
  const email = `system-creator-content-${Date.now()}@example.com`

  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Content Workflow Creator')
  await page.locator('label:has-text("Email") input').fill(email)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.locator('label:has-text("I am a") select').selectOption('Creator')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')

  await page.click('text=Manage tiers')
  await expect(page).toHaveURL(/\/tiers/)

  const tierName = `System Tier ${Date.now()}`
  await page.locator('label:has-text("Name") input').fill(tierName)
  await page.locator('label:has-text("Description") textarea').fill('Created by a system test')
  await page.locator('label:has-text("Price") input').fill('7.50')
  await page.click('button:has-text("Create tier")')

  await expect(page.locator(`text=${tierName}`)).toBeVisible()

  await page.click('text=New post')
  await expect(page).toHaveURL(/\/content\/new/)

  const postTitle = `System Test Post ${Date.now()}`
  await page.locator('label:has-text("Title") input').fill(postTitle)
  await page.locator('label:has-text("Body") textarea').fill('Body written by a Playwright system test.')
  await page.click('button:has-text("Publish")')

  await expect(page).toHaveURL('/')
  await expect(page.locator(`text=${postTitle}`)).toBeVisible()
})
