# Conventions

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
- **Begin the transaction before any read that participates in a write sequence** — any `FirstOrDefaultAsync` or `AnyAsync` whose result is used to decide whether to insert, update, or delete must be inside the transaction. Reads that are purely for 404 guards (existence checks with no corresponding write) may stay outside. Placing the read outside the transaction and the write inside creates a race window where the read result is stale by the time the write executes.
- **Use `ExecuteDeleteAsync` instead of `Remove` + `SaveChangesAsync` when the deleted entity drives a counter update** — `ExecuteDeleteAsync` returns the number of rows actually deleted. Guard the counter update on `deletedCount > 0` to prevent double-decrements under concurrent requests. Example: `RemoveReaction`, `UnfollowChannel`. Never load a collection into memory just to delete it; use `ExecuteDeleteAsync` with a `Where` filter instead.
- **Use `ExecuteUpdateAsync` with a guard condition for idempotent state transitions** — when a state transition must happen exactly once (e.g. soft-delete), perform it as `ExecuteUpdateAsync(WHERE current_state)` and check the returned row count. Only apply side effects (counter updates, etc.) if the count is greater than zero. This prevents double side-effects when two concurrent requests race through the same guard check.
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

## Code readability

- **Named variables over inline expressions** — always assign the result of a check or query to a descriptively named variable before using it in a condition. Never inline results directly into `if` statements. This applies to both async calls and boolean logic.
  ```csharp
  // ✅
  var emailAlreadyRegistered = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
  if (emailAlreadyRegistered) return Errors.User.EmailAlreadyInUse();

  // ❌
  if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct)) return Errors.User.EmailAlreadyInUse();
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

## Route parameter conventions

- **User identity in routes** — always use `user.Username` (a `string`), never `user.Id` (a `Guid`). Route segment: `{username}`.
- **Channel identity in routes** — always use `channel.Handle` (a `string`), never `channel.Id` (a `Guid`). Route segment: `{handle}`.
- **Channel routes are always scoped under their user** — `/v1/users/{username}/channels/{handle}/...`. Never expose a flat `/v1/channels/{channelId}/...` route for channel-scoped resources.
- **Handler lookup pattern** — resolve the channel by `(handle, username)` pair:
  ```csharp
  var channel = await db.Channels
      .FirstOrDefaultAsync(c => c.Handle == query.Handle && c.User.Username == query.Username, ct);
  ```
- **Playlist routes** — playlists are user-scoped: `GET /v1/users/{username}/playlists`. Channel playlists add a handle: `GET /v1/users/{username}/channels/{handle}/playlists`.

## Testing conventions

- **Domain entities must always have unit tests** — placed in `tests/VidroApi.UnitTests/Domain/<EntityName>Tests.cs`.
- **Features must always have integration tests** — placed in `tests/VidroApi.IntegrationTests/<Domain>/<FeatureName>Tests.cs`.
- Integration tests use `ApiFactory` (`WebApplicationFactory<Program>` + Testcontainers PostgreSQL) and exercise the full HTTP stack.
- Use `IClassFixture<ApiFactory>` to share the container across tests in a class. Generate unique usernames/emails per test (e.g. `Guid.NewGuid()`) to avoid inter-test conflicts.
- Assert on both HTTP status code and response body (`code` field for errors, `data` for success).
- **Test helper pattern for channel creation** — `CreateChannelAndGetIds()` returns `(string AccessToken, string Username, string ChannelHandle)`. Video creation helpers take `username` and `channelHandle`, not IDs.
