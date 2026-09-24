# Campfire

Self-hosted creator membership platform (personal Patreon alternative).

## Structure

- `src/Campfire.Api` — ASP.NET Core Web API (EF Core + PostgreSQL, Stripe.NET)
- `src/Campfire.Client` — React + Vite + TypeScript frontend
- `docker-compose.yml` — local PostgreSQL instance

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

## Admin account

Admin accounts can't be created via `/api/auth/register` (only `Creator`/`Member`
can self-register). Instead, the API seeds a single Admin on startup from the
`AdminSeed` config section, if one doesn't already exist. In dev,
`appsettings.Development.json` ships with a default (`admin@campfire.local` /
`dev-only-admin-password-change-me`) — change it for anything beyond local dev:

```bash
cd src/Campfire.Api
dotnet user-secrets set "AdminSeed:Email" "you@example.com"
dotnet user-secrets set "AdminSeed:Password" "a-real-password"
```
