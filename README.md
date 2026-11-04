# Longevity Diet Companion

## Project description

PRN232 Final Assignment: distributed .NET application hỗ trợ người dùng xây dựng và theo dõi thói quen longevity diet/lifestyle theo hướng giải thích được, truy vết được rule/source và có distributed architecture rõ ràng.

## Product boundary

Đây là ứng dụng **wellness / education / adherence**, không phải medical device.

Hệ thống không:
- chẩn đoán bệnh;
- dự đoán tuổi thọ hoặc biological age;
- kê thuốc;
- tự tạo phác đồ điều trị;
- cho AI override allergy, exclusion hoặc safety rule.

## Technology stack

- Frontend: React 19 + TypeScript + Vite.
- REST API: ASP.NET Core .NET 9.
- Layering: API -> Services -> Repository -> EF Core -> SQL Server.
- Authentication: JWT access token + rotating refresh token.
- Internal service: gRPC Recommendation Service.
- Async messaging: Redis Streams.
- Background processing: .NET Worker Service.
- Database: SQL Server 2022.
- Containerization: Docker + Docker Compose.
- API documentation: OpenAPI + Swagger UI.
- Logging: Serilog.
- Optional local AI: explanation rewrite only; never safety/ranking authority.

## Repository structure

```text
PRN232_LongevityDiet/
├─ .agents/
│  └─ skills/
├─ docs/
│  ├─ adr/
│  ├─ architecture/
│  └─ task/
│     ├─ Week 1/
│     ├─ ...
│     └─ Week 4/
├─ scripts/
├─ src/
│  ├─ LongevityDiet.API/
│  ├─ LongevityDiet.Domain/
│  ├─ LongevityDiet.Repositories/
│  ├─ LongevityDiet.Services/
│  ├─ LongevityDiet.Recommendation.Grpc/
│  ├─ LongevityDiet.Worker/
│  └─ LongevityDiet.Web/
├─ tests/
│  ├─ LongevityDiet.UnitTests/
│  ├─ LongevityDiet.IntegrationTests/
│  └─ LongevityDiet.E2ETests/
├─ .editorconfig
├─ .github/
│  ├─ pull_request_template.md
│  └─ workflows/project-quality.yml
├─ CONTRIBUTING.md
├─ Directory.Build.props
├─ docker-compose.yml
├─ docker-compose.dcproj
├─ .env.example
├─ LongevityDiet.slnLaunch
└─ LongevityDiet.sln
```

`LongevityDiet.Web` is a Visual Studio JavaScript Project System (`.esproj`) project. Frontend and backend remain separate deployable applications. Generated folders such as `bin/`, `obj/`, `node_modules/` and `dist/` are not source and are ignored.

## System architecture

- `docs/assignment/README.md` - Assignment-facing documentation pack.
- `docs/assignment/00-ASSIGNMENT-DOCUMENT.md` - submission entry point: Context -> Problems -> Solutions -> Actors/Features -> C0/C1 -> Technology -> Conceptual ERD -> Physical DB -> PRN232 coverage.
- `docs/05-SYSTEM-ARCHITECTURE.md` - detailed engineering architecture.
- `docs/architecture/README.md` - extended internal architecture diagram set.
- `docs/architecture/02-container-architecture.drawio`
- `docs/10-PRN232-TRACEABILITY.md`

The canonical diagram set covers:
- System Context;
- Container Architecture;
- Docker Deployment;
- API Component view;
- Recommendation dynamic flow;
- Transactional Outbox + Redis flow;
- PRN232 requirement coverage;
- deliverables/demo assessment;
- end-to-end demo flow;
- Production Secure Deployment.

## Current implementation status

### Completed engineering baseline

- research, scope, business/functional/non-functional requirements;
- safety/privacy/AI guardrails;
- PRN232 and book-to-software traceability;
- 10 canonical architecture diagrams with geometry/export QA;
- .NET 9 solution with separate ASP.NET Core backend and React/TypeScript/Vite `.esproj` frontend;
- Visual Studio multi-project startup profile for Web + API;
- separate development ports: Web `5173`, API HTTPS `7110` / HTTP `5110`;
- Visual Studio Docker Compose project for Web/API/gRPC/Worker/SQL/Redis;
- OpenAPI/Swagger and health baseline;
- EF Core/JWT/gRPC/Redis/Serilog dependencies;
- repository-wide `.editorconfig` + .NET analyzer/build policy.

### Week 1 Task 1 completed

- User, UserProfile and RefreshToken domain/persistence;
- EF Core DbContext + IdentityFoundation migration;
- register/login/refresh/revoke;
- JWT access token;
- rotating refresh token with SHA-256 hash persisted in SQL;
- HttpOnly refresh cookie;
- profile read/update and onboarding foundation;
- React AuthProvider, protected routes, login/register/onboarding/profile;
- premium landing page integrated with auth-aware navigation;
- 8 unit tests;
- 7 HTTP integration tests using `WebApplicationFactory<Program>` + SQLite in-memory.
- 1 Playwright + Chrome browser E2E test covering Register -> Onboarding -> App -> session restore -> Profile persistence -> Logout -> protected redirect.

Week 1 Task 1 is complete. The remaining Week 1 Task 2-4 work is documented under `docs/task/Week 1/`.

## Task planning

Canonical planning location:

- `docs/task/ROADMAP-4-WEEKS.md`
- `docs/task/Week 1/README.md` through `docs/task/Week 4/README.md`
- each week contains `Task 1.md` through `Task 4.md`

Week 1 Task 1 was assigned to Thành viên 1 (bạn) and is complete. All tasks are end-to-end Full-Stack tasks and include reviewer, dependencies, testing, deliverables and Definition of Done.

## Team Member Responsibilities

The team has four Full-Stack Developers. Ownership is organized by end-to-end task, not by a permanent frontend/backend silo. Each Owner is responsible for the complete vertical slice required by the task and each task has a different Reviewer.

| Member | Primary responsibility | Week 1 ownership | Review responsibility |
|---|---|---|---|
| Thành viên 1 | Identity/authentication foundation, profile/onboarding, integration quality and project consistency | Task 1 - Identity, Authentication, Profile & Onboarding | Reviews Task 3 |
| Thành viên 2 | Catalog/data-query foundation and business data quality | Task 2 - Food, Recipe, Diet Rule Catalog & Query Foundation | Reviews Task 4 |
| Thành viên 3 | Core planning/tracking workflow and deterministic business behavior | Task 3 - Meal Planning, Meal Logging & Eating Window | Reviews Task 1 |
| Thành viên 4 | Distributed-service thin slice and asynchronous processing | Task 4 - gRPC Recommendation + Outbox + Redis Streams + Worker | Reviews Task 2 |

Shared responsibilities for every member:
- follow the active `docs/task/Week N/Task N.md` scope and Definition of Done;
- preserve the canonical architecture and dependency direction;
- implement the full vertical slice where the task requires Domain/Data -> Repository/Service -> API -> Web -> Tests;
- add/update tests, migrations, API contracts and documentation affected by the change;
- run project-structure validation, build and relevant tests before requesting review;
- never commit secrets or generated build artifacts;
- review AI-generated code before merge and verify it runs in the real project;
- perform cross-review before feature -> `develop` merge.

Detailed contribution and architecture rules are defined in `CONTRIBUTING.md` and reinforced by `AGENTS.md`, `.editorconfig`, the pull-request template and `scripts/Validate-ProjectStructure.ps1`.

## Installation guide

Open `LongevityDiet.sln` in Visual Studio 2022. The solution contains separate backend (`.csproj`) and frontend (`.esproj`) projects.

For normal development, select **Development - Full Stack** and press F5. Visual Studio starts SQL Server and Redis through Docker Compose, while Web, REST API, Recommendation gRPC and Worker run as native development projects for fast debugging.

```text
Frontend UI:         http://localhost:5173
Backend API HTTPS:   https://localhost:7110
Backend API HTTP:    http://localhost:5110
Recommendation gRPC http://localhost:5010
SQL Server:          localhost:14330
Redis:               localhost:6379
Swagger:             https://localhost:7110/swagger/index.html
Health:              https://localhost:7110/health
```

During native development, Vite proxies `/api/*` from port `5173` to the backend at `https://localhost:7110`. Frontend and backend therefore remain separate processes and separate ports while browser requests keep a simple same-origin development flow.

Use **Development - Web + API** only when SQL Server/Redis are already running and you only need the UI/API pair. For a fully containerized runtime, select **Docker Compose - Full Stack**. Visual Studio uses `docker-compose.dcproj` to start Web, API, Recommendation gRPC, Worker, SQL Server and Redis. Docker ports are Web `5173`, API `8080`, gRPC `8081`, SQL Server `14330`, and Redis `6379`.

## Local development configuration

Secrets are not stored in `appsettings.Development.json`.

For the current developer machine, the API SQL connection string/JWT signing key and the Worker SQL connection string are stored with .NET User Secrets.

For a new machine, run the repository initializer once before the first F5:

```powershell
.\scripts\Initialize-LocalDevelopment.ps1
```

It creates a local ignored `.env` with generated development secrets when needed and synchronizes the API/Worker .NET User Secrets. Do not commit `.env`.

## Build and test

```powershell
cd D:\PRN232\PRN232_LongevityDiet
dotnet restore
dotnet build .\LongevityDiet.sln
dotnet test .\LongevityDiet.sln --no-build
```

Frontend:
```powershell
cd .\src\LongevityDiet.Web
npm ci
npm run build
```

Browser E2E:
```powershell
cd .\tests\LongevityDiet.E2ETests
npm ci
npm test
```

Against an already running Docker Compose full stack:
```powershell
npm run test:docker
```

Current verified test baseline:
- UnitTests: 8/8 pass.
- IntegrationTests: 7/7 pass.
- E2ETests: 1/1 pass on Chrome in native Visual Studio-style development.
- Docker E2ETests: 1/1 pass against the fully containerized stack.
- Full .NET build: 0 warnings, 0 errors.
- Docker Compose build/runtime smoke: PASS.

## Deployment instructions

```powershell
Copy-Item .env.example .env
docker compose up -d --build
docker compose ps
```

Core containers:
- web;
- api;
- recommendation-grpc;
- worker;
- sqlserver;
- redis.

## PRN232 mandatory mapping

- REST API: `LongevityDiet.API`.
- Layering: API -> Services -> Repository.
- CRUD + search/filter/sort/pagination: Week 1 Task 2.
- JWT: Week 1 Task 1.
- Dependency Injection: ASP.NET Core/Generic Host DI with scoped DbContext and centralized service registration.
- Configuration management: appsettings + environment variables + .NET User Secrets/local ignored .env.
- Logging & exception handling: structured logging + RFC7807/ProblemDetails.
- Background Service: `LongevityDiet.Worker`.
- Redis Streams producer/consumer: Week 1 Task 4.
- gRPC Recommendation: Week 1 Task 4.
- Docker: Compose + Dockerfiles.
- Database: SQL Server + EF Core.
- Swagger/OpenAPI: API baseline.
- Full rubric/evidence matrix: docs/10-PRN232-TRACEABILITY.md.
- Test/deploy/final-demo plan: docs/09-TEST-DEPLOY-DEMO.md.

## Research basis

The project distinguishes:
1. PRN232 assignment requirements;
2. book/official longevity-diet principles;
3. peer-reviewed evidence;
4. project-defined software heuristics such as LDAS.

See `docs/01-BOOK-RESEARCH.md` and `docs/11-BOOK-TO-SOFTWARE-TRACEABILITY.md`.


## Extended C2 architecture, complete SQL diagrams and Cloudflare session prototype (November 2026)

Editable deliverables under [`docs/assignment/extended-diagrams`](docs/assignment/extended-diagrams):

- [C2 Component Architecture, 8 pages](docs/assignment/extended-diagrams/05-c2-component-architecture.drawio) — one Current API page + seven explicitly Target pages.
- [All Database Diagrams, 9 pages](docs/assignment/extended-diagrams/06-all-database-erd.drawio) — physical Current SQL, six logical target SQL DBs, plus optional future Cloudflare D1 session store.
- [C2 PDF](docs/assignment/extended-diagrams/05-c2-component-architecture.pdf) and [Database PDF](docs/assignment/extended-diagrams/06-all-database-erd.pdf); 17 PNG pages in `extended-diagrams/previews/`.
- [Data model rationale and 33/33 table mapping](docs/assignment/06-C2-AND-DATABASE-MAPPING.md).
- [Cloudflare D1 security/migration guide](docs/security/CLOUDFLARE-REFRESH-TOKENS.md) and [proposed Worker source](cloudflare/refresh-sessions/README.md).

**Current runtime remains ONE SQL database, containing three EF Core entity tables.** The six SQL databases, split services and most of the 33 target entity types are design-only, not deployed. The Cloudflare refresh-session store is implemented as a **feature-flagged, OFF-by-default prototype**, never deployed without a configured Cloudflare account and complete E2E validation. The existing SQL refresh-token behavior remains the default.

Validation: `powershell -File scripts/Validate-C2DbDiagrams.ps1`, `node --test cloudflare/refresh-sessions/tests/worker.test.mjs`, `dotnet test tests/LongevityDiet.IntegrationTests -c Release`.

To opt in *after provisioning and verifying* a Cloudflare Worker + D1, set secret environment variables `CloudflareSessions__Enabled=true`, `CloudflareSessions__BaseUrl=https://YOUR_PRIVATE_WORKER_HOST/` and `CloudflareSessions__ServiceBearer=<FROM_SECURE_SECRET_MANAGER>`; do **not** put credentials into Git or `appsettings.json`. See the migration guide before switching live sessions.
