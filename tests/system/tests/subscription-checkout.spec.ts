import { test, expect } from '@playwright/test'

// Workflow: a Member subscribes to a Creator's tier through the real Stripe
// Elements checkout, using Stripe's own always-succeeds test card. Covers
// REQ-006 (subscribe and pay) against Stripe's real test-mode sandbox, not a
// mock, matching the plan's "manual checkout walkthrough" formalized as an
// automated system test.
//
// Requires real Stripe test-mode keys configured (see README): skips rather
// than fails when Stripe:SecretKey / the Stripe CLI webhook forwarder are not
// set up, since CI and a fresh clone won't have them.

test('a Member can subscribe to a tier with a real Stripe test card and see it reflected as their current plan', async ({ page }) => {
  // Real network round trips to Stripe (setup intent, Elements iframe load,
  // confirm) plus the webhook round trip make this the slowest test in the
  // suite by a wide margin; the default 30s budget is too tight on a machine
  // also running Postgres, the API, Vite, and the Stripe CLI forwarder.
  test.setTimeout(60_000)
  const ts = Date.now()

  // Creator + a priced tier
  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Checkout Creator')
  await page.locator('label:has-text("Email") input').fill(`checkout-creator-${ts}@example.com`)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.locator('label:has-text("I am a") select').selectOption('Creator')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')

  await page.click('text=Manage tiers')
  const tierName = `Checkout Tier ${ts}`
  await page.locator('label:has-text("Name") input').fill(tierName)
  await page.locator('label:has-text("Description") textarea').fill('For system test checkout')
  await page.locator('label:has-text("Price") input').fill('4.99')
  await page.click('button:has-text("Create tier")')
  await expect(page.locator(`text=${tierName}`)).toBeVisible()
  await page.click('text=Log out')

  // Member subscribes
  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Checkout Member')
  await page.locator('label:has-text("Email") input').fill(`checkout-member-${ts}@example.com`)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')
  await page.click('text=Membership')
  await expect(page).toHaveURL('/membership')

  const tierCard = page.locator('.tier-card', { hasText: tierName })
  await tierCard.locator('button:has-text("Subscribe")').click()

  await expect(page.locator('.modal-overlay')).toBeVisible()
  // Stripe Elements loads its own JS bundle and renders the iframe async; on a
  // loaded dev machine (several other services running) that can take longer
  // than a typical UI wait, so this is deliberately generous rather than flaky.
  const cardFrame = page.frameLocator('iframe[title="Secure card payment input frame"]').first()
  await cardFrame.locator('[name="cardnumber"]').fill('4242424242424242', { timeout: 30_000 })
  await cardFrame.locator('[name="exp-date"]').fill('12/34')
  await cardFrame.locator('[name="cvc"]').fill('123')
  await cardFrame.locator('[name="postal"]').fill('37601')

  await page.click('button:has-text("Subscribe for")')

  // the modal closes once the client-side payment confirmation succeeds
  await expect(page.locator('.modal-overlay')).toBeHidden({ timeout: 15_000 })
  await expect(page.locator('text=Subscribed! Your membership is now active.')).toBeVisible()

  // but the subscription only flips to Active in our DB once Stripe's webhook
  // round-trips (via the Stripe CLI forwarder in dev); the dashboard only
  // refetches on mount, so reload rather than expecting the badge to appear
  // on its own.
  await expect(async () => {
    await page.reload()
    await expect(tierCard.locator('text=Current plan')).toBeVisible({ timeout: 2_000 })
  }).toPass({ timeout: 20_000 })
})
