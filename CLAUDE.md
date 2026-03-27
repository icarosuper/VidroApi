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
- **Domain methods for state mutations** — e.g. `video.MarkAsReady(...)`, `channel.IncrementFollowerCount()`, `user.ChangeEmail(...)`. Keeps logic encapsulated instead of spreading it across slices.
- **No value objects** unless a type has real validation/equality rules (none identified yet)
- **Base classes:** `BaseEntity` (Id + CreatedAt, both `init`) and `BaseAuditableEntity : BaseEntity` (+ `UpdatedAt` with `private set`, mutated via `SetUpdatedAt(now)`)
- **Errors live in `Domain/Errors/`**. Three kinds:
  - `CommonErrors` — generic, parameterized: `CommonErrors.NotFound("User", id)`, `CommonErrors.Unauthorized()`, `CommonErrors.Forbidden()`
  - `EntityErrors/Errors.X.cs` — one file per entity, `static partial class Errors` with a nested static class per entity. Called as `Errors.User.IncorrectPassword()`, `Errors.Channel.NotOwner()`
  - `FeatureErrors/Errors.X.cs` — same pattern for cross-entity feature errors (e.g. upload flow)
  - Each `Error` carries a `Code`, `Message`, and `ErrorType` (enum). The Api layer maps `ErrorType` to HTTP status codes in `ResultExtensions`.

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
dotnet ef migrations add <Name> --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/VidroApi.Infrastructure --startup-project src/VidroApi.Api

# Start dependencies
docker-compose up -d postgres redis minio
```

## Architecture

**Clean Architecture + Vertical Slice Architecture.** Each feature lives in a single self-contained file under `src/VidroApi.Application/<Domain>/FeatureName.cs`.

### Project dependency flow

```
Domain ← Application ← Infrastructure ← Api
```

- **Domain** — entities, enums, `DomainError`. No external dependencies.
- **Application** — one file per feature (slice). Defines interfaces (`IMinioService`, `IJobQueueService`) that Infrastructure implements. No EF Core here.
- **Infrastructure** — EF Core `AppDbContext`, `MinioService`, `RedisJobQueueService`, `TokenService`, `DateTimeProvider`, settings classes. All external I/O lives here. Entity mappings use `IEntityTypeConfiguration<T>` (one file per entity in `Persistence/Configurations/`). `OnModelCreating` applies `DeleteBehavior.Restrict` globally for all FKs.
- **Api** — `Program.cs` only. Registers DI, middleware, JWT, and calls `FeatureName.MapEndpoint(app)` for every slice.

### Vertical Slice pattern

Every slice in `Application/` follows this structure:

```csharp
public static class FeatureName
{
    public record Request(...) : IRequest<Result<Response, Error>>;
    public record Response(...);
    public class Validator : AbstractValidator<Request> { ... }
    public class Handler(IDateTimeProvider clock, ...) : IRequestHandler<Request, Result<Response, Error>> { ... }
    public static void MapEndpoint(IEndpointRouteBuilder app) => app.MapPost(...);
}
```

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
