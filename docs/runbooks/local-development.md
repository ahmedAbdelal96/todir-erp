# Local development runtime

Start the backend and frontend in two terminals. From the repository root, run the backend launcher; it starts local infrastructure, builds the backend once, applies migrations and the repeatable development seed, then starts the API and Worker without rebuilding shared projects.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/backend.ps1
```

In a second terminal, start Vite:

```powershell
Set-Location frontend
npm run dev
```

Open `http://127.0.0.1:5173/`. The API health endpoint is `http://127.0.0.1:5080/health`. Press `Ctrl+C` in the backend terminal to stop both backend hosts; stop the frontend in its terminal separately.

Connection strings and development-only runtime settings belong in the ignored `backend/appsettings.Development.json`; they must not be committed. Compose administrator credentials are supplied only through the current PowerShell process environment and are never stored in the repository. The backend launcher reads local runtime credentials from that file, targets the Compose database named `todir`, and aligns the local runtime role password with the configured password. It does not overwrite the settings file.

Before building, the launcher stops existing API/Worker processes from this checkout to prevent duplicate hosts and shared-assembly locks. If another application owns port `5080`, startup stops with its process ID instead of trying to launch a second API.

## First-time setup

Create one local development configuration file at `backend/appsettings.Development.json`:

```powershell
Copy-Item backend/appsettings.json backend/appsettings.Development.json
```

Replace the connection string and local service credentials in that one file. The API, Worker, and DbMigrator all read the shared files `backend/appsettings.json` and `backend/appsettings.Development.json`.

Set disposable local service credentials in the current PowerShell session. Use different values for the PostgreSQL administrator, RabbitMQ, and MinIO credentials, and do not paste them into tracked files:

```powershell
$env:TODIR_DEV_POSTGRES_ADMIN_PASSWORD = '<local-admin-secret-at-least-16-chars>'
$env:TODIR_DEV_RABBITMQ_PASSWORD = '<local-rabbit-secret-at-least-16-chars>'
$env:TODIR_DEV_OBJECT_STORAGE_PASSWORD = '<local-object-storage-secret-at-least-16-chars>'
```

The local Compose service accounts are PostgreSQL `postgres`, RabbitMQ `todir`, and MinIO `todir`; their passwords are supplied by those environment variables. The database port is `15432` and RabbitMQ is `5672`. These are development-only services and are loopback-bound by Compose; they are not production credentials or a production deployment pattern.

Vite proxies `/api` requests to the API.

The canonical development accounts are `admin@atlas.com` and `admin@delta.com`; both use the password supplied as `SEED_DEFAULT_PASSWORD` during seeding.
