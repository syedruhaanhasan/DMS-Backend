# WDAS — Local Development Guide

This guide walks you through starting the WDAS API on your machine for local development.

## Prerequisites

| Requirement | Notes |
|-------------|--------|
| **.NET 10 SDK** | Run `dotnet --version` — should report `10.x`. |
| **SQL Server** | Default config uses **LocalDB**: `(localdb)\mssqllocaldb`. Install via [Visual Studio](https://visualstudio.microsoft.com/) or [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads). |
| **EF Core tools** (optional) | Restore from repo root: `dotnet tool restore` |

## Quick start

From the repository root (`d:\document management`):

```powershell
# 1. Restore packages
dotnet restore WDAS.slnx

# 2. (Optional) Apply migrations manually — startup also migrates automatically
dotnet tool restore
dotnet ef database update --project src/WDAS.Infrastructure --startup-project src/WDAS.Api

# 3. Start the API
dotnet run --project src/WDAS.Api
```

The server listens on:

| Profile | URL |
|---------|-----|
| HTTP | http://localhost:5110 |
| HTTPS | https://localhost:7067 |

Use the **https** profile explicitly if you want TLS:

```powershell
dotnet run --project src/WDAS.Api --launch-profile https
```

## What happens on startup

1. **Database** — `DatabaseSeeder` runs `MigrateAsync()` (falls back to `EnsureCreatedAsync` if migration fails).
2. **Seed data** — On first run, demo users, departments, and a **Purchase Request** workflow (Finance) are inserted.
3. **Identity** — `Identity:Provider` is set to `Dev` (see `appsettings.json`), so login uses seeded credentials — no real LDAP/AD required.

## Verify the server

| Check | URL / command |
|-------|----------------|
| Health | `GET http://localhost:5110/health` |
| **Swagger UI** | **http://localhost:5110/swagger** (Development only) |
| OpenAPI JSON | `GET http://localhost:5110/swagger/v1/swagger.json` |
| Tests | `dotnet test WDAS.slnx` |

## Authentication

All API endpoints except login require a JWT. Obtain a token:

```http
POST http://localhost:5110/api/auth/login
Content-Type: application/json

{
  "username": "maker.owner",
  "password": "Owner123!"
}
```

Use the returned `accessToken` as a Bearer token:

```http
Authorization: Bearer <accessToken>
```

### Using Swagger UI

1. Open **http://localhost:5110/swagger**
2. Call **POST /api/auth/login** with `maker.owner` / `Owner123!`
3. Copy the `accessToken` value from the response
4. Click **Authorize** (top right), paste the token (no `Bearer ` prefix needed), then **Authorize**
5. Try any protected endpoint (e.g. `GET /api/workflows`)

### Seeded users

| Username | Password | Role |
|----------|----------|------|
| `super.admin` | `Super123!` | SuperAdmin |
| `finance.admin` | `Finance123!` | DepartmentAdmin |
| `maker.owner` | `Owner123!` | MakerOwner |
| `approver.one` | `Approver123!` | Approver |
| `approver.two` | `Approver123!` | Approver |
| `auditor.user` | `Auditor123!` | Auditor |

## Try an end-to-end flow

1. **Login** as `maker.owner`.
2. **List workflows**: `GET /api/workflows` — pick **Purchase Request**.
3. **Create & submit** a document:

```http
POST /api/documents
Authorization: Bearer <owner-token>
Content-Type: application/json

{
  "workflowId": "<purchase-request-workflow-id>",
  "toRecipients": "Finance Leadership",
  "subject": "Laptop purchase",
  "bodyHtml": "<p>Requesting approval.</p>",
  "amount": null,
  "priority": "Normal",
  "recipients": [],
  "adHocApproverUserIds": null,
  "submit": true,
  "idempotencyKey": "demo-submit-1"
}
```

4. **Approve step 1** as `approver.one`: `POST /api/workflow-steps/{stepId}/approve`
5. **Approve step 2** as `approver.two` — document moves to **ReadyForFinalization**.

## Configuration

Edit `src/WDAS.Api/appsettings.json`. Both **PostgreSQL** and **SQL Server** databases are supported — pick one with `Database:Provider`:

```json
{
  "Database": {
    "Provider": "PostgreSql"
  },
  "ConnectionStrings": {
    "PostgreSql": "Host=localhost;Port=5432;Database=DocumentManagementSystem;Username=postgres;Password=YOUR_PASSWORD;",
    "SqlServer": "Server=.\\SQLEXPRESS;Database=DocumentManagementSystem;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

| `Database:Provider` | Uses connection string |
|---------------------|-------------------------|
| `PostgreSql` | `ConnectionStrings:PostgreSql` |
| `SqlServer` | `ConnectionStrings:SqlServer` |
| `Sqlite` | `ConnectionStrings:Sqlite` (optional) |

To switch databases, change only `Database:Provider` and restart the API.

### Create both databases (Windows)

From the repository root:

```powershell
.\backend\scripts\create-databases.ps1
```

This creates `DocumentManagementSystem` on PostgreSQL and SQL Server Express (`.\SQLEXPRESS`) if they do not already exist.

### SQLite (no server install)

Add a `Sqlite` connection string and set `"Provider": "Sqlite"`:

```json
"ConnectionStrings": {
  "Sqlite": "Data Source=wdas-dev.db"
}
```

### Legacy `DefaultConnection`

If `Database:Provider` is omitted, `ConnectionStrings:DefaultConnection` is still used as a fallback.

### Switch to LDAP (production-style)

Set `Identity:Provider` to `Ldap` and configure LDAP settings in `appsettings.json` (see Infrastructure LDAP provider).

## Useful commands

```powershell
# Build
dotnet build WDAS.slnx

# Run tests
dotnet test WDAS.slnx

# Add a new migration (after entity changes)
dotnet ef migrations add <MigrationName> --project src/WDAS.Infrastructure --startup-project src/WDAS.Api

# Reset LocalDB database (destructive)
dotnet ef database drop --project src/WDAS.Infrastructure --startup-project src/WDAS.Api --force
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `Cannot open database` / LocalDB not found | Install SQL Server Express LocalDB or point `DefaultConnection` at a running SQL Server instance. |
| Port already in use | Change ports in `src/WDAS.Api/Properties/launchSettings.json` or stop the other process. |
| `401 Unauthorized` | Login again; ensure the `Authorization: Bearer` header is set. |
| Empty workflows list | Delete the DB and restart so seed runs again, or call `POST /api/auth/sync` as `super.admin`. |
| HTTPS certificate warning | Use the `http` profile or trust the dev certificate: `dotnet dev-certs https --trust` |

## API reference

Phase 1 endpoints are documented in the OpenAPI spec at `/openapi/v1.json`. Import that URL into [Postman](https://www.postman.com/) or use the **REST Client** / **Thunder Client** VS Code extensions.
