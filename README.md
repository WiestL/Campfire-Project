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
