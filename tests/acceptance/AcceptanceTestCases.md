# Campfire — Acceptance Test Cases (Draft)

Status: **Draft.** Case design is complete; execution is manual and in progress. Cases marked "Covered by System suite" have also
been exercised automatically as a side effect of the Playwright System tests
in `tests/system/`, since those already drive the real UI end to end; the
rest are intended to be walked through manually against the live stack before
Week 15, per the V&V Plan's "criteria from the PID's user requirements,
walked through per persona."

Each case gives the user story it validates, the steps a real person would
take, and the expected result from that person's point of view — not an
implementation detail. Pass/Fail is recorded as cases are actually run.

## Creator persona

| ID | User story | Steps | Expected result | Status |
|----|------------|-------|------------------|--------|
| ACC-C1 | As a new creator, I want to sign up and immediately see my own dashboard, so I know my account exists and I know what to do next. | 1. Go to /register. 2. Fill in name, email, password. 3. Select "Creator". 4. Submit. | Lands on the Creator Dashboard, not a blank page or the Member view. Dashboard clearly shows "Content", "Membership Tiers", and "Analytics" as next steps. | Covered by System suite (auth.spec.ts) |
| ACC-C2 | As a creator, I want to define a paid membership tier with a name, description, and price, so members have something to subscribe to. | 1. Log in as a Creator. 2. Go to Tiers. 3. Fill in name/description/price. 4. Create. | New tier appears immediately in "Your tiers" with the price shown, no page reload needed. | Covered by System suite (content-and-tiers.spec.ts) |
| ACC-C3 | As a creator, I want to publish a post and have it show up right away, so I know members can actually see it. | 1. Log in as a Creator. 2. New post. 3. Fill in title/body. 4. Publish. | Redirected to the dashboard; the new post is visible at the top of the feed immediately. | Covered by System suite (content-and-tiers.spec.ts) |
| ACC-C4 | As a creator, I want to restrict a post to a specific tier, so only paying members at that level see the full content. | 1. New post. 2. Set Visibility to a specific tier. 3. Publish. | Members without that tier (or a lower one) see only a locked teaser; the creator's own view of the post is always unlocked. | Covered by System suite (tier-gated-access.spec.ts) |
| ACC-C5 | As a creator, I want to upload a video and have it actually play back for members, not just show a broken link. | 1. New post, type Video. 2. Choose a real video file. 3. Publish. 4. View the post as a member. | Video loads and plays in the feed; scrubbing works (range requests). | Manual — pending |

## Member persona

| ID | User story | Steps | Expected result | Status |
|----|------------|-------|------------------|--------|
| ACC-M1 | As a new member, I want to register without being asked for payment info up front, so I can browse for free first. | 1. Go to /register. 2. Leave role at the default. 3. Submit. | Account is created and I land on the Member Dashboard with no card/payment step forced before I can look around. | Covered by System suite (auth.spec.ts) |
| ACC-M2 | As a member, I want to see what a creator's tiers cost and what they include before I pay anything. | 1. Log in as a Member. 2. Scroll to "Choose your membership". | Each tier card shows name, price, and description before any payment screen appears. | Covered by System suite (content-and-tiers.spec.ts) |
| ACC-M3 | As a member, I want to subscribe with a real card and know clearly that it worked, not be left guessing. | 1. Click Subscribe on a tier. 2. Enter card details. 3. Submit. | A clear "Subscribed! Your membership is now active." message appears, and the tier I subscribed to is marked "Current plan" shortly after. | Covered by System suite (subscription-checkout.spec.ts) |
| ACC-M4 | As a member without a subscription, I want it to be obvious which posts I can't see yet and why, not just missing or broken. | 1. Log in as a Member with no subscription. 2. Scroll the feed past a gated post. | The post still shows its title and who posted it, but is clearly marked "Locked" with a plain-language reason ("Subscribe to X to unlock") instead of silently disappearing or showing an error. | Covered by System suite (tier-gated-access.spec.ts) |
| ACC-M5 | As a member, if my card is declined, I want a clear error and a way to back out, not a stuck screen. | 1. Click Subscribe. 2. Enter a Stripe test card that is declined (4000000000000002). 3. Submit. | A plain-language error appears in the modal, and a visible Close/Cancel button lets me leave without refreshing the page. | Manual — pending |
| ACC-M6 | As a member, I want to cancel my subscription and have the app reflect that I'm no longer a paying member. | 1. Subscribe to a tier. 2. (Cancel flow, once built.) | Current plan badge disappears; previously-gated content returns to locked. | Manual — pending (cancellation UI not yet built; tracked as a scope gap, see status summary) |

## Admin persona

| ID | User story | Steps | Expected result | Status |
|----|------------|-------|------------------|--------|
| ACC-A1 | As an admin, I want to see every tier-gated post regardless of my own subscriptions, so I can moderate the platform. | 1. Log in as the seeded Admin account. 2. View the feed, including a tier-gated post from any creator. | The post is fully visible (title, body, media), never shown as locked. | Manual — pending |
| ACC-A2 | As a platform owner, I want to know admin accounts can't just be created by anyone registering, so the role can't be self-granted. | 1. Go to /register. 2. Check the role selector. | Only "Member" and "Creator" are offered; there is no way to register directly as Admin from the public form. | Manual — pending |

