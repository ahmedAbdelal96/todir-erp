# Local development runtime

The local runtime uses the checked-in Docker Compose infrastructure and host-run API, Worker, and Vite processes. Connection strings and development-only service credentials belong in each host's ignored `appsettings.Development.json`; they must not be passed on every command line or committed.

## First-time setup

Create one local development configuration file at `backend/appsettings.Development.json`:

```powershell
Copy-Item backend/appsettings.json backend/appsettings.Development.json
```

Replace the connection string and local service credentials in that one file. The API, Worker, and DbMigrator all read the shared files `backend/appsettings.json` and `backend/appsettings.Development.json`.

The repository's local Docker defaults are PostgreSQL `postgres/postgres`, RabbitMQ `todir/todir-dev-only`, database port `15432`, and RabbitMQ port `5672`. These values are development-only and are not production credentials.

## Start infrastructure

```powershell
docker compose up -d
docker compose ps
```

## Apply migrations and seed development data

Run this when the database is new or when the development seed needs to be refreshed. The password is supplied only for the explicit seed operation and is never stored in source control:

```powershell
$env:SEED_DEFAULT_PASSWORD = "<local development password>"
dotnet run --project backend/DbMigrator/DbMigrator.csproj -- --seed development
```

## Start the application

Use separate terminals from the repository root:

```powershell
dotnet run --project backend/Api/Api.csproj
```

```powershell
dotnet run --project backend/Worker/Worker.csproj
```

```powershell
corepack pnpm install --frozen-lockfile
corepack pnpm --dir frontend dev
```

Open `http://127.0.0.1:5173/`. The API is available at `http://127.0.0.1:5080/health`, and Vite proxies `/api` requests to that API.

The canonical development accounts are `admin@atlas.com` and `admin@delta.com`; both use the password supplied as `SEED_DEFAULT_PASSWORD` during seeding.
