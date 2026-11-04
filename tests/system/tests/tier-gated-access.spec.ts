import { test, expect } from '@playwright/test'

// Workflow + security check: a Creator gates a post behind a paid tier. A
// Member with no subscription must see only a locked teaser, never the real
// body or media. Covers REQ-007 end to end, and doubles as the plan's "basic
// security check" for this feature: the assertion below fails if the body
// text ever leaks into the page, not just if the UI merely fails to render it
// nicely, which is the failure mode that would actually matter.

test('a Member with no subscription sees a locked teaser, never the gated post body, for tier-restricted content', async ({ page }) => {
  const ts = Date.now()
  const secretBody = `SECRET-BODY-${ts}-should-never-reach-an-unsubscribed-member`
  const postTitle = `Gated System Test Post ${ts}`

  // Creator: tier + gated post
  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Gating Creator')
  await page.locator('label:has-text("Email") input').fill(`gating-creator-${ts}@example.com`)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.locator('label:has-text("I am a") select').selectOption('Creator')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')

  await page.click('text=Manage tiers')
  const tierName = `Gating Tier ${ts}`
  await page.locator('label:has-text("Name") input').fill(tierName)
  await page.locator('label:has-text("Description") textarea').fill('Gating tier for system test')
  await page.locator('label:has-text("Price") input').fill('9.99')
  await page.click('button:has-text("Create tier")')
  await expect(page.locator(`text=${tierName}`)).toBeVisible()

  await page.click('text=New post')
  await page.locator('label:has-text("Title") input').fill(postTitle)
  await page.locator('label:has-text("Body") textarea').fill(secretBody)
  await page.locator('label:has-text("Visibility") select').selectOption({ label: `${tierName} and above` })
  await page.click('button:has-text("Publish")')
  await expect(page).toHaveURL('/')
  await page.click('text=Log out')

  // an unrelated Member, never subscribed to this creator
  await page.goto('/register')
  await page.locator('label:has-text("Display name") input').fill('Gating Member')
  await page.locator('label:has-text("Email") input').fill(`gating-member-${ts}@example.com`)
  await page.locator('label:has-text("Password") input').fill('password123')
  await page.click('button[type="submit"]')
  await expect(page).toHaveURL('/')

  const lockedCard = page.locator('.feed-item', { hasText: postTitle })
  await expect(lockedCard).toBeVisible()
  await expect(lockedCard.locator('text=Locked')).toBeVisible()
  await expect(lockedCard.locator(`text=${tierName}`)).toBeVisible()

  // the security-relevant assertion: the secret body must not be anywhere in
  // the rendered page, not merely hidden behind CSS
  await expect(page.locator('body')).not.toContainText(secretBody)

  // and not anywhere in the raw HTML either, which a CSS-only hide would still leak
  const html = await page.content()
  expect(html).not.toContain(secretBody)
})
