# Architecture

## Overview

**Clean Architecture + Vertical Slice Architecture.** Each feature lives in a single self-contained file under `src/VidroApi.Api/Features/<Domain>/FeatureName.cs`.

Features live in the Api project (not Application) so they can freely access `AppDbContext`, BCrypt, and other Infrastructure types without creating circular dependencies. Application holds shared abstractions, behaviors, and `PagedResult` only.

### Project dependency flow

```
Domain ← Application ← Infrastructure ← Api
```

- **Domain** — entities, enums, `DomainError`. No external dependencies.
- **Application** — defines interfaces (`IMinioService`, `IJobQueueService`) that Infrastructure implements. No EF Core here.
- **Infrastructure** — EF Core `AppDbContext`, `MinioService`, `RedisJobQueueService`, `TokenService`, `DateTimeProvider`, settings classes. All external I/O lives here. Entity mappings use `IEntityTypeConfiguration<T>` (one file per entity in `Persistence/Configurations/`). `OnModelCreating` applies `DeleteBehavior.Cascade` globally for all FKs.
- **Api** — `Program.cs` only. Registers DI, middleware, JWT, calls `FeatureName.MapEndpoint(app)` for every slice, and registers background services (`BackgroundServices/`).

## Vertical Slice pattern

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
- `IDateTimeProvider` is injected into handlers that need the current time, which is then passed to entity constructors.

## Response format

```json
// Success (200/201)
{ "data": { ... } }

// Error (400/401/403/404/409)
{ "code": "user.not_found", "message": "User with id '...' was not found." }
```

`ApiResponse` and `ResultExtensions` live in `Api/Common/` and `Api/Extensions/`.

## Enums in responses

**Never return a raw enum string or a raw integer.** Always use `EnumValue` (`Api/Common/EnumValue.cs`) so the consumer gets both the numeric ID and the human-readable string:

```json
{ "visibility": { "id": 0, "value": "Public" } }
```

In handlers (non-LINQ context), use the static helper:
```csharp
Visibility = EnumValue.From(video.Visibility),
```

In EF Core LINQ projections (`.Select(v => ...)`), use inline construction because `EnumValue.From` is a custom method that cannot be translated to SQL:
```csharp
Visibility = new EnumValue { Id = (int)v.Visibility, Value = v.Visibility.ToString() },
```

## Auth

DIY JWT — no ASP.NET Core Identity. `TokenService` (Infrastructure) generates access tokens (15 min) and refresh tokens (7 days). Refresh tokens are stored in `RefreshTokens` table and rotated on each use. Extract `UserId` from claims using `ctx.User.GetUserId()` (extension on `ClaimsPrincipal`).

## Integration with VideoProcessor (Go)

The VideoProcessor is a separate service at `../VideoProcessor`. Integration points:

1. **Upload** — API writes raw video to MinIO at `raw/{videoId}` via presigned PUT URL (client uploads directly, never through the API).
2. **Enqueue** — `IJobQueueService.PublishJobAsync(videoId, callbackUrl)` writes a `job:{videoId}` key to Redis and pushes `videoId` to `video_queue`.
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

## Key configuration sections (`appsettings.json`)

- `ConnectionStrings:Postgres`, `ConnectionStrings:Redis`
- `MinIO` — endpoint, credentials, bucket, `UploadUrlTtlHours`
- `Jwt` — secret, token expiry
- `VideoSettings:MaxTagsPerVideo` — validated in slices, not hardcoded
- `VideoSettings:ReconciliationIntervalMinutes` — interval for `VideoReconciliationService`
- `TrendingSettings` — score weights and time decay for `GET /videos/trending`
- `Webhook:Secret` — HMAC secret shared with VideoProcessor
- `StorageCleanupSettings:IntervalMinutes`, `StorageCleanupSettings:BatchSize` — controls `StorageCleanupService`
