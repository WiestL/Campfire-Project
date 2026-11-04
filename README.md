# Campfire

Self-hosted creator membership platform (personal Patreon alternative).

## Structure

- `src/Campfire.Api`: ASP.NET Core Web API (EF Core + PostgreSQL, Stripe.NET)
- `src/Campfire.Client`: React + Vite + TypeScript frontend
- `docker-compose.yml`: local PostgreSQL instance

## Prerequisites

- .NET 10 SDK
- Node 24+
- Docker (for local PostgreSQL)

## Getting started

```bash
# start Postgres (maps to host port 5433 to avoid clashing with other local instances)
docker compose up -d

# apply EF Core migrations
dotnet tool restore
dotnet dotnet-ef database update --project src/Campfire.Api --startup-project src/Campfire.Api

# run the API (https://localhost:7058)
dotnet run --project src/Campfire.Api

# run the frontend (http://localhost:5173)
cd src/Campfire.Client
npm install
npm run dev
```

Set Stripe keys via user secrets rather than `appsettings.json`:

```bash
cd src/Campfire.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
```

Unlike media storage, there is no local fallback for payments: subscription
checkout (`POST /api/subscriptions/setup-intent`, `POST /api/subscriptions`) and
the webhook handler (`POST /api/stripe/webhook`) need real Stripe test-mode keys
to function at all. Without them, those endpoints return a clean `502` rather
than crashing, but nothing will actually process. To receive webhooks locally,
forward them with the [Stripe CLI](https://stripe.com/docs/stripe-cli):

```bash
stripe listen --forward-to https://localhost:7058/api/stripe/webhook
```

That command prints a webhook signing secret to use for `Stripe:WebhookSecret`.

## Admin account

Admin accounts can't be created via `/api/auth/register` (only `Creator`/`Member`
can self-register). Instead, the API seeds a single Admin on startup from the
`AdminSeed` config section, if one doesn't already exist. In dev,
`appsettings.Development.json` ships with a default (`admin@campfire.local` /
`dev-only-admin-password-change-me`); change it for anything beyond local dev:

```bash
cd src/Campfire.Api
dotnet user-secrets set "AdminSeed:Email" "you@example.com"
dotnet user-secrets set "AdminSeed:Password" "a-real-password"
```

## Media delivery (CDN)

Video/audio uploads go through `IMediaStorage`, with two implementations:

- **Local** (default): saves to `wwwroot/media` and serves it back through
  `MediaController`, which validates a signed, time-limited URL using the same
  token-auth scheme Bunny.net uses. No external account needed for local dev.
- **Bunny**: uploads to a Bunny.net Storage Zone and signs Pull Zone URLs for
  playback. Enable it once you have a Bunny.net account:

```bash
cd src/Campfire.Api
dotnet user-secrets set "Cdn:Provider" "Bunny"
dotnet user-secrets set "Cdn:StorageZoneName" "your-zone"
dotnet user-secrets set "Cdn:StorageApiKey" "your-storage-api-key"
dotnet user-secrets set "Cdn:PullZoneHostname" "your-zone.b-cdn.net"
dotnet user-secrets set "Cdn:SigningKey" "your-pull-zone-token-auth-key"
```

Signed URLs default to a 1-hour expiry (`Cdn:UrlExpirySeconds`), generated fresh
on every `/api/content` response rather than stored statically.

## Testing

Four levels, per the project's V&V Plan (`docs/vv/`, not committed; see the
term project submission). Status and current metrics: `tests/Week11-ProgressCheck.md`.

### Unit tests

No external dependencies; uses EF Core InMemory and mocked Stripe/CDN calls.

```bash
dotnet test src/Campfire.Api.Tests -- NUnit.Where="cat == 'Unit Tests'"
```

### Integration tests

Needs the real Postgres container running (`docker compose up -d`) and, for
the Bunny/Stripe cases, real sandbox credentials already set via
`dotnet user-secrets` as described above. Each fixture skips gracefully
(not fails) when its dependency isn't reachable, so a partial setup still
runs whatever it can:

```bash
docker compose up -d
dotnet test src/Campfire.Api.Tests -- NUnit.Where="cat == 'Integration Tests'"
```

Run everything (Unit + Integration) with plain `dotnet test src/Campfire.Api.Tests`.

> `--filter TestCategory=...` (VSTest's own filter syntax) can't parse a
> category name containing a space, which is why these use NUnit's own
> `NUnit.Where` instead.

### System tests

Playwright against the live stack: start Postgres, the API
(`dotnet run --project src/Campfire.Api`), and the frontend
(`npm run dev` in `src/Campfire.Client`) first. The subscription-checkout
spec also needs the Stripe CLI forwarding webhooks:

```bash
stripe listen --forward-to https://localhost:7058/api/stripe/webhook
```

Then, from `tests/system/`:

```bash
cd tests/system
npm install
npx playwright install chromium
npx playwright test
```

### Acceptance tests

Draft criteria, derived per-persona from the project's requirements, in
`tests/acceptance/AcceptanceTestCases.md`. Several are already exercised by
the System suite above; the rest are intended as manual walkthroughs.

### Code coverage

```bash
dotnet test src/Campfire.Api.Tests --collect:"XPlat Code Coverage"
reportgenerator -reports:"src/Campfire.Api.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```

(`reportgenerator`: `dotnet tool install -g dotnet-reportgenerator-globaltool`.)
