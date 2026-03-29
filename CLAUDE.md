# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Domain entity conventions

- **Public constructor** with required fields + `DateTimeOffset now` as last parameter (passed up via `: base(now)`). All business-value properties must be initialized in the constructor body — including defaults like `FollowerCount = 0` or `IsRevoked = false`. The `= null!` on string/navigation properties is a nullability annotation for the EF Core parameterless constructor and is not considered a real initialization.
- **Parameterless constructor for EF Core** — always `private` (or `protected` for abstract base classes), decorated with `// ReSharper disable once UnusedMember.Local` and `[ExcludeFromCodeCoverage]`
- **`init` for immutable properties** (e.g. `Username`, `CreatedAt`, `Id`); **`private set` for mutable ones** (e.g. `Email`, `PasswordHash`, counters). EF Core hydrates both via reflection — no `public set` needed.
- **Nullable properties** (`string?`, `DateTimeOffset?`) never need `= null` — it's the implicit default. Only `= null!` on non-nullable strings/navigations (to suppress CS8618 from the EF Core parameterless constructor).
- **Navigation collections** — backing field with `// ReSharper disable once CollectionNeverUpdated.Local` to suppress the IDE warning (EF Core populates via reflection):
  ```csharp
  // ReSharper disable once CollectionNeverUpdated.Local
  private readonly List<Video> _videos = [];
  public IReadOnlyList<Video> Videos => _videos.AsReadOnly();
  ```
- **Domain methods for state mutations** — e.g. `video.MarkAsReady(...)`, `user.ChangeEmail(...)`. Keeps logic encapsulated instead of spreading it across slices.
- **No counter methods on entities** — counters (`LikeCount`, `DislikeCount`, `ViewCount`, `FollowerCount`) are updated atomically via `ExecuteUpdateAsync` in the feature handler. Entity methods like `IncrementLikeCount()` are not safe under concurrency and must not be added.
- **`ExecuteUpdateAsync` always goes in a private method** — never inline in `Handle`. Name the method after its intent (e.g. `IncrementFollowerCount`, `DecrementFollowerCount`). Return `Task<int>` (not `Task`) to match the return type of `ExecuteUpdateAsync` exactly and avoid implicit upcasting overhead.
- **Use a transaction whenever `SaveChangesAsync` and `ExecuteUpdateAsync` must be atomic** — wrap both in `await using var tx = await db.Database.BeginTransactionAsync(ct)` and call `await tx.CommitAsync(ct)` at the end. The `await using` ensures automatic rollback on failure.
- **No value objects** unless a type has real validation/equality rules (none identified yet)
- **Base classes:** `BaseEntity` (Id + CreatedAt, both `init`) and `BaseAuditableEntity : BaseEntity` (+ `UpdatedAt` with `private set`, mutated via `SetUpdatedAt(now)`)
- **Errors live in `Domain/Errors/`**. Three kinds:
  - `CommonErrors` — generic, parameterized: `CommonErrors.NotFound("User", id)`, `CommonErrors.Unauthorized()`, `CommonErrors.Forbidden()`
  - `EntityErrors/Errors.X.cs` — one file per entity, `static partial class Errors` with a nested static class per entity. Called as `Errors.User.IncorrectPassword()`, `Errors.Channel.NotOwner()`
  - `FeatureErrors/Errors.X.cs` — same pattern for cross-entity feature errors (e.g. upload flow)
  - Each `Error` carries a `Code`, `Message`, and `ErrorType` (enum). The Api layer maps `ErrorType` to HTTP status codes in `ResultExtensions`.

## Settings / POCO conventions

- **Non-nullable `string` properties** use `= null!` (not `= default!`) to suppress CS8618 while making the intent clear.
- **Acronyms in property names** follow PascalCase with only the first letter capitalized: `UseSsl`, `HlsPath`, `ApiKey` — never `UseSSL`, `HLSPath`, `APIKey`.
- **No default values in Settings POCOs** — defaults belong in `appsettings.json`, not in code. Code defaults silently mask missing configuration. The Settings POCO is just a typed shell; validation at startup (`ValidateOnStart()`) catches missing values early.

## Branching and release strategy

- **Feature branches** — one branch per feature group (e.g. `feature/auth`, `feature/channels`, `feature/videos`), branching off `master` and merged back via PR.
- **`master`** — always deployable. CI/CD deploys `master` HEAD to staging automatically.
- **Releases** — marked with a git tag (`v1.0.0`, `v1.1.0`, etc.) on `master`. Production deploys from tags.
- **No staging branches** — no `staging/vX.Y.Z` branches. Rollback is done by redeploying a previous tag.
- **Coordination with VidroProcessor** — when a change affects the shared contract (MinIO paths, Redis queue name, webhook format), both repos must be tagged and deployed together.

## Code readability

- **Named variables over inline expressions** — always assign the result of a check or query to a descriptively named variable before using it in a condition. Never inline results directly into `if` statements. This applies to both async calls and boolean logic.
  ```csharp
  // ✅
  var emailAlreadyRegistered = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
  if (emailAlreadyRegistered) return Errors.User.EmailAlreadyInUse();

  var isTokenExpiredOrRevoked = token.IsRevoked || token.ExpiresAt < clock.UtcNow;
  if (isTokenExpiredOrRevoked) return Errors.RefreshToken.Invalid();

  // ❌
  if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct)) return Errors.User.EmailAlreadyInUse();
  if (token.IsRevoked || token.ExpiresAt < clock.UtcNow) return Errors.RefreshToken.Invalid();
  ```
- **Extract complex or long logic into private methods** — if a block of code needs a comment to explain what it does, it should be a method with a name that explains it instead.
- **`Handle` should read like a sequence of named steps** — any non-trivial inline block (query building, object construction, projection/mapping) must be extracted to a private method. The goal is that `Handle` reads top-to-bottom as a series of descriptive calls with no implementation detail.
- **`SaveChangesAsync` always stays in `Handle`** — private methods must never call `db.SaveChangesAsync`. They should only stage changes (e.g. `db.Add`, `db.Remove`). This keeps the persistence boundary explicit and visible in the handler.
- **Build `Response` inline in `Handle`** — only extract the mapping to a private method if the `Response` is very large (many fields across multiple related objects). For typical responses, keep the `new Response { ... }` directly in `Handle` so the return value is explicit.
- **Method and variable names must express intent** — the name should answer "what" not "how". Avoid abbreviations, single-letter names (outside loops), and generic names like `result`, `data`, `temp`.
- **No `Async` suffix on method names** — the return type (`Task`/`ValueTask`) already communicates that. Never name a method `DoSomethingAsync`.
- **Ternaries always span three lines** — condition on the first line, `?` branch on the second, `:` branch on the third. Never write a ternary on a single line.
  ```csharp
  // ✅
  Guid? requestingUserId = user.Identity?.IsAuthenticated == true
      ? user.GetUserId()
      : null;

  // ❌
  Guid? requestingUserId = user.Identity?.IsAuthenticated == true ? user.GetUserId() : null;
  ```
- **Prefer `{}` block body over `=>` expression body** for methods with more than one line. Reserve `=>` for truly one-line methods (e.g. computed properties on entities, simple delegating calls).
  ```csharp
  // ✅ one-liner → =>
  public int Total => Items.Count;

  // ✅ multi-line → {}
  private Task<Video?> FetchVideo(Guid id, CancellationToken ct)
  {
      return db.Videos.Include(v => v.Channel).FirstOrDefaultAsync(v => v.Id == id, ct);
  }

  // ❌ multi-line with =>
  private Task<Video?> FetchVideo(Guid id, CancellationToken ct) =>
      db.Videos.Include(v => v.Channel).FirstOrDefaultAsync(v => v.Id == id, ct);
  ```

## Testing conventions

- **Domain entities must always have unit tests** — placed in `tests/VidroApi.UnitTests/Domain/<EntityName>Tests.cs`.
- **Features must always have integration tests** — placed in `tests/VidroApi.IntegrationTests/<Domain>/<FeatureName>Tests.cs`.
- Integration tests use `ApiFactory` (`WebApplicationFactory<Program>` + Testcontainers PostgreSQL) and exercise the full HTTP stack.
- Use `IClassFixture<ApiFactory>` to share the container across tests in a class. Generate unique usernames/emails per test (e.g. `Guid.NewGuid()`) to avoid inter-test conflicts.
- Assert on both HTTP status code and response body (`code` field for errors, `data` for success).

## Language

All code must be in English: class names, method names, variables, test names, log messages, comments, and XML docs. The only exception is commit messages, which are written in Portuguese.

## Working style

After each implementation step:
1. **Suggest a commit message in Portuguese** — the user reviews and commits manually. Never commit without being asked.
2. **Show the next possible steps** — brief list so the user can choose what to implement next.

## Commands

```bash
# Build
dotnet build

# Run API (development)
dotnet run --project src/VidroApi.Api

# All tests
dotnet test

# Single test class
dotnet test tests/VidroApi.UnitTests --filter "FullyQualifiedName~ClassName"

# Single test method
dotnet test tests/VidroApi.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"

# EF Core migrations (always specify both projects)
# Migration names must follow the pattern: PascalCaseDescriptionMigration (e.g. AddCommentsFeatureMigration, ChangeUsernameMaxLengthMigration)
dotnet ef migrations add <DescriptionMigration> --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api

# Start dependencies
docker-compose up -d postgres redis minio
```

## Architecture

**Clean Architecture + Vertical Slice Architecture.** Each feature lives in a single self-contained file under `src/VidroApi.Api/Features/<Domain>/FeatureName.cs`.

Features live in the Api project (not Application) so they can freely access `AppDbContext`, BCrypt, and other Infrastructure types without creating circular dependencies. Application holds shared abstractions, behaviors, and `PagedResult` only.

### Project dependency flow

```
Domain ← Application ← Infrastructure ← Api
```

- **Domain** — entities, enums, `DomainError`. No external dependencies.
- **Application** — one file per feature (slice). Defines interfaces (`IMinioService`, `IJobQueueService`) that Infrastructure implements. No EF Core here.
- **Infrastructure** — EF Core `AppDbContext`, `MinioService`, `RedisJobQueueService`, `TokenService`, `DateTimeProvider`, settings classes. All external I/O lives here. Entity mappings use `IEntityTypeConfiguration<T>` (one file per entity in `Persistence/Configurations/`). `OnModelCreating` applies `DeleteBehavior.Restrict` globally for all FKs.
- **Api** — `Program.cs` only. Registers DI, middleware, JWT, and calls `FeatureName.MapEndpoint(app)` for every slice.

### Vertical Slice pattern

Every slice in `Api/Features/<Domain>/` follows this structure — in this exact order:

```csharp
public static class FeatureName
{
    public record Request : IRequest<Result<Response, Error>>
    {
        public string Foo { get; init; } = null!;
    }

    public record Response
    {
        public Guid Id { get; init; }
    }

    public class Validator : AbstractValidator<Request> { ... }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/v1/...", async (Request req, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(req, ct);
            return result.ToApiResult(StatusCodes.Status201Created);
        });

    public class Handler(AppDbContext db, IDateTimeProvider clock, ...)
        : IRequestHandler<Request, Result<Response, Error>>
    {
        public async Task<Result<Response, Error>> Handle(Request req, CancellationToken ct) { ... }
    }
}
```

- **Order within a slice:** Request → Response → Validator → MapEndpoint → Handler.
- **`Request` vs `Command`** — use `Request : IRequest<...>` when the body is the only input source. When the handler needs data from multiple sources (e.g. body + JWT claims), use `Request` for the HTTP body and a separate `Command : IRequest<...>` for the Mediator message; the endpoint constructs `Command` from both. Example: `SignOut` has `Request { RefreshToken }` (body) and `Command { UserId, RefreshToken }` (body + claim).
- **Request/Response format:** `record` with `init` properties (one per line), not positional records.
- **No magic numbers in Validators** — use constants from the domain entity (e.g. `User.UsernameMinLength`, `User.PasswordMinLength`). Add the same constants to the entity constructor guards too.
- **Endpoint registration is automatic** — `app.MapAllEndpoints()` in `Program.cs` scans the assembly by reflection and calls every public static `MapEndpoint` method. Never register endpoints manually.
- **`result.ToApiResult(statusCode)`** maps `Result<T, Error>` to the correct HTTP response — success wraps in `{ "data": ... }`, failure maps `ErrorType` to status code and returns `{ "code": ..., "message": ... }`.

- Handlers return `Result<Response, Error>` (CSharpFunctionalExtensions). Use `Result.Success(response)` or `DomainError.X.Y()` (which is an `Error`).
- Validation runs automatically via `ValidationBehavior<,>` (MediatR pipeline). FluentValidation exceptions are caught by middleware and returned as 400.
- In endpoint handlers call `.ToApiResult()` (from `Api/Extensions/ResultExtensions.cs`) to convert the result to `IResult`.
- `IDateTimeProvider` is injected into handlers that need the current time, which is then passed to entity constructors.

### Response format

```json
// Success (200/201)
{ "data": { ... } }

// Error (400/401/403/404/409)
{ "code": "user.not_found", "message": "User with id '...' was not found." }
```

`ApiResponse` and `ResultExtensions` live in `Api/Common/` and `Api/Extensions/`.

### Integration with VideoProcessor (Go)

The VideoProcessor is a separate service at `../VideoProcessor`. Integration points:

1. **Upload** — API writes raw video to MinIO at `raw/{videoId}` via presigned PUT URL (client uploads directly, never through the API).
2. **Enqueue** — `IJobQueueService.PublishJobAsync(videoId, callbackUrl)` writes a `job:{videoId}` key to Redis and pushes `videoId` to `PROCESSING_REQUEST_QUEUE`.
3. **Webhook** — VideoProcessor calls `POST /webhooks/video-processed` when done. Validated with HMAC-SHA256 (`X-Webhook-Signature: sha256=...`). Secret is shared via `Webhook:Secret` config.

### MinIO object paths (shared contract with VideoProcessor)

| Path | Owner | Content |
|---|---|---|
| `raw/{videoId}` | API | Original upload |
| `processed/{videoId}_processed` | VideoProcessor | Transcoded video |
| `thumbnails/{videoId}/` | VideoProcessor | 5 JPG frames |
| `audio/{videoId}.mp3` | VideoProcessor | Audio track |
| `preview/{videoId}_preview.mp4` | VideoProcessor | Low-quality preview |
| `hls/{videoId}/` | VideoProcessor | HLS segments + playlist |

### Auth

DIY JWT — no ASP.NET Core Identity. `TokenService` (Infrastructure) generates access tokens (15 min) and refresh tokens (7 days). Refresh tokens are stored in `RefreshTokens` table and rotated on each use. Extract `UserId` from claims using `ctx.User.GetUserId()` (extension on `ClaimsPrincipal`).

### Key configuration sections (`appsettings.json`)

- `ConnectionStrings:Postgres`, `ConnectionStrings:Redis`
- `MinIO` — endpoint, credentials, bucket, `UploadUrlTtlHours`
- `Jwt` — secret, token expiry
- `VideoSettings:MaxTagsPerVideo` — validated in slices, not hardcoded
- `TrendingSettings` — score weights and time decay for `GET /videos/trending`
- `Webhook:Secret` — HMAC secret shared with VideoProcessor

## Design decisions

- **Counters are denormalized** — `LikeCount`, `DislikeCount`, `ViewCount` on `Videos` and `FollowerCount` on `Channels` are updated atomically via `ExecuteUpdateAsync`. Never use `COUNT(*)` for these.
- **Cursor-based pagination everywhere** — use `CreatedAt` as cursor, never `OFFSET`.
- **Videos belong to Channels, not Users** — `Video.ChannelId → Channel.UserId`. A user can own multiple channels.
- **`VideoArtifacts` and `VideoMetadata` are separate tables** — 1:1 with `Videos`, nullable until processing completes.
- **Single presigned PUT URL for upload** — multipart is planned but not implemented. See `docs/plans/` for future work.

## Implementation plan

See `docs/plans/2026-03-26-implementation-plan.md` for the full task-by-task plan.
