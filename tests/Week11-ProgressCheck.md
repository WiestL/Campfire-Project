# Week 11 Progress Check — Implementation and Test Status

CSCI 5220 V&V Term Project — Campfire · Luke Wiest

This covers the Week 11 milestone: current source code, completed unit and
integration test suites, draft system and acceptance test cases, an interim
metrics snapshot, and this status/risk summary.

## Test suite status

| Level | Status | Count | Tooling |
|---|---|---|---|
| Unit | Complete, passing | 97 | NUnit, EF Core InMemory, Moq |
| Integration | Complete, passing | 12 | NUnit, real Postgres (Npgsql) + real Bunny.net/Stripe sandboxes |
| System | Draft, passing | 7 | Playwright against the live stack |
| Acceptance | Draft, partially executed | 12 cases (5 covered by System suite, 7 manual-pending) | `tests/acceptance/AcceptanceTestCases.md` |

Total automated: 116 tests (109 via `dotnet test`, 7 via `npx playwright test`).

**Unit** (`src/Campfire.Api.Tests/Unit/`): isolates controller decision logic
behind fakes (EF InMemory, mocked `IStripeSubscriptionService`), one typical
and one edge/invalid case per decision point, run automatically in CI on
every push.

**Integration** (`src/Campfire.Api.Tests/Integration/`): the same code
against real adjacent systems instead of fakes, per the plan's bottom-up
strategy —
- `DatabaseConstraintIntegrationTests` (6 tests): real Postgres via Docker,
  exercising the unique email index, FK cascade/restrict rules, and decimal
  column precision — none of which EF InMemory enforces, so these are things
  the Unit suite structurally cannot catch.
- `BunnyCdnIntegrationTests` (3 tests): real upload/signed-URL/tamper checks
  against Bunny.net's live CDN.
- `StripeIntegrationTests` (3 tests): real customer/setup-intent/subscription
  creation against a Stripe test-mode account.

These skip gracefully (not fail) when Postgres/Bunny/Stripe aren't reachable,
so CI — which has none of them — stays on the Unit suite only
(`NUnit.Where="cat == 'Unit Tests'"`, documented in `.github/workflows/ci.yml`).

**System** (`tests/system/`): Playwright driving the real UI against the
real API, real Postgres, and a real Stripe test-mode checkout — one spec per
major workflow (auth, content/tiers, subscription checkout, tier-gated
access). Marked draft per the milestone's own allowance, since this is the
newest suite and the checkout flow in particular depends on external network
timing; see Risks below.

**Acceptance** (`tests/acceptance/AcceptanceTestCases.md`): criteria derived
per-persona (Creator/Member/Admin) from the project's own requirement set,
including the required usability consideration. 5 of 12 cases are already
exercised as a side effect of the System suite; the remaining 7 (decline
handling, cancellation, admin views) are written but not yet manually run.

## Interim metrics snapshot — code coverage

Collected via `dotnet test --collect:"XPlat Code Coverage"` + ReportGenerator,
2026-11-03. Two numbers, deliberately: Unit-only is what CI measures on every
push; combined is what the project actually has covered once Integration is
counted, since classes like `StripeSubscriptionService` and
`BunnyMediaStorage` are intentionally exercised by Integration tests against
real services, not Unit tests against fakes — reading Unit coverage alone
would make those look untested when they aren't.

| Metric | Unit only | Unit + Integration |
|---|---|---|
| Line coverage | 30.9% (610/1971) | 74.2% (1464/1971) |
| Branch coverage | 36.9% (164/444) | 41.4% (184/444) |
| Method coverage | 73.7% (166/225) | 79.5% (179/225) |

Notable per-class gaps in the combined number: `UsersController` was at 0%
until this pass (closed with 2 new tests, one per decision point — found user
vs. no matching record); `Program.cs` and EF migration files are expected to
stay near 0%, since they're startup wiring and generated schema code, not
logic. Branch coverage (41.4%) trailing line coverage (74.2%) is the honest
signal here: a meaningful share of conditional branches, mostly in DTOs and
response-shaping code, aren't independently exercised yet. Closing that gap
is the main coverage-focused work planned before Week 15, rather than
chasing the line-coverage number higher on its own.

## Defects found this cycle, by severity/priority

Using the course's Severity (Extreme–Cosmetic) / Priority (Highest–Lowest)
scales. All are fixed as of this snapshot except where noted.

| # | Defect | Found via | Severity | Priority | Status |
|---|---|---|---|---|---|
| 1 | Subscription creation requested a 5-level Stripe API expand; Stripe caps at 4, so the request was rejected outright. | Running the real checkout flow against Stripe (pre-dates this milestone) | Extreme | Highest | Fixed |
| 2 | Subscriptions only collected payment for 3D-Secure cases; Stripe's normal flow needs client confirmation every time, so every subscription would have silently stalled as Incomplete. | Running the real checkout flow against Stripe | Extreme | Highest | Fixed |
| 3 | A newly created Stripe account defaults to a newer API version than the installed SDK expects, breaking webhook signature verification. | Running the real webhook flow against Stripe | Major | High | Fixed |
| 4 | A 144 MB upload was rejected immediately: ASP.NET Core's multipart form-size limit is separate from, and lower than, the request-size limit already raised. | Manual upload testing | Major | High | Fixed |
| 5 | **Race condition:** concurrent requests for the same user's Stripe customer (e.g. two browser-tab checkouts, or React StrictMode's dev-mode double effect invocation) could each see no customer on file and create two separate Stripe customers; whichever write "lost" left the database pointing at a different customer than the one the user's payment method was actually attached to, surfacing several requests later as a cryptic "payment method already attached to a customer" error. | **System test suite, running checkout concurrently across parallel Playwright workers** | Extreme | Highest | Fixed — `SubscriptionsController.EnsureStripeCustomerAsync` now serializes customer creation per user with a re-check after acquiring the lock |
| 6 | `UsersController.Me` had no test coverage at all (0%). | Reading the coverage report | Minor | Medium | Fixed — 2 new unit tests, one per decision point |

Defect #5 is the most significant finding of this cycle and is the clearest
evidence so far that System-level testing earns its place in the plan: it is
a real concurrency bug that no Unit test (fakes, single-threaded by
construction) or Integration test (one request at a time) could have
surfaced, and it was found purely because the System suite happened to run
four checkout-adjacent browser sessions in parallel.

## Risks and scope adjustments

- **Subscription cancellation has no UI yet.** Acceptance case ACC-M6 is
  blocked on this; tracked as a scope gap, not a defect, since the feature
  was never built rather than built-and-broken. Planned before Week 15.
- **The System checkout spec is network-timing sensitive.** It depends on
  Stripe Elements' iframe load time and the Stripe CLI webhook forwarder
  being up locally; both are real external dependencies, not flakiness in
  the app itself, but the test needed a generous (60s) timeout to stop
  reporting false failures on a loaded dev machine. Documented in the spec
  file itself; will reassess if it proves unstable in CI-like conditions.
- **Branch coverage (41.4%) lags behind line coverage (74.2%).** Treated as
  the real coverage priority over the next few weeks, rather than the
  higher-level line number.
- **Acceptance testing is partially manual by design.** Five of twelve cases
  are already covered by System automation; the other seven (card decline
  handling, cancellation, admin-specific views) are written as draft cases
  and scheduled for manual walkthroughs, consistent with the assignment's
  "may be manual, scripted, or automated."
- **No second reviewer.** This remains solo work; acceptance criteria are
  checked against the project's own written requirement set rather than a
  second person's judgment, same mitigation stated in the Week 6 plan.

## AI use disclosure

Per my professor's explicit permission for this project, discussed in a
personal meeting (also noted in the Week 6 V&V Plan draft): Claude
(Anthropic) was used to build the application, write the Unit, Integration,
and System test suites, draft the Acceptance test cases, and draft this
status document. I reviewed and take responsibility for all of it, including
verifying the reported test results and coverage numbers against actual
`dotnet test` and `npx playwright test` runs on my own machine rather than
taking them on faith.
