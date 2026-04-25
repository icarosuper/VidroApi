# VidroApi

**Vidro** — [VidroProcessor](../Processor) · [VidroFront](../FrontNovo)

---

REST API for Vidro, a video platform. Built with .NET, Clean Architecture + Vertical Slice.

## Stack

- **.NET 10** — ASP.NET Core minimal API
- **PostgreSQL** — primary database (EF Core)
- **Redis** — job queue (video processing)
- **MinIO** — object storage (videos, thumbnails, avatars)
- **MediatR** — request handling per slice
- **JWT** — auth (access + refresh tokens)

## Architecture

```
Domain ← Application ← Infrastructure ← Api
```

Each feature is a single self-contained file: `src/VidroApi.Api/Features/<Domain>/FeatureName.cs`.

## Quick Start

```bash
# Start dependencies
docker-compose up -d postgres redis minio

# Run API
dotnet run --project src/VidroApi.Api
```

Default: `http://localhost:5000`

## Commands

```bash
dotnet build
dotnet run --project src/VidroApi.Api
dotnet test
dotnet test tests/VidroApi.UnitTests --filter "FullyQualifiedName~ClassName"

# EF Core migrations
dotnet ef migrations add <DescriptionMigration> \
  --project src/VidroApi.Infrastructure \
  --startup-project src/VidroApi.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/VidroApi.Infrastructure \
  --startup-project src/VidroApi.Api
```

## API Endpoints

### Auth
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/auth/signup` | Register |
| POST | `/v1/auth/signin` | Login |
| POST | `/v1/auth/signout` | Logout |
| POST | `/v1/auth/renew-token` | Refresh access token |

### Users
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/users/me` | Current user |
| POST | `/v1/users/me/avatar` | Upload avatar |

### Channels
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/channels` | Create channel |
| GET | `/v1/users/{username}/channels/{handle}` | Get channel |
| GET | `/v1/users/{username}/channels` | List user channels |
| PUT | `/v1/channels/{handle}` | Update channel |
| DELETE | `/v1/channels/{handle}` | Delete channel |
| POST | `/v1/users/{username}/channels/{handle}/follow` | Follow |
| DELETE | `/v1/users/{username}/channels/{handle}/follow` | Unfollow |
| POST | `/v1/channels/{handle}/avatar` | Upload channel avatar |

### Videos
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/channels/{channelId}/videos` | Create video |
| GET | `/v1/videos/{videoId}` | Get video |
| PUT | `/v1/videos/{videoId}` | Update video |
| DELETE | `/v1/videos/{videoId}` | Delete video |
| GET | `/v1/channels/{channelId}/videos` | List channel videos |
| GET | `/v1/feed` | Feed |
| GET | `/v1/videos/trending` | Trending |
| GET | `/v1/videos/search` | Search |
| POST | `/v1/videos/{videoId}/react` | React |
| DELETE | `/v1/videos/{videoId}/react` | Remove reaction |
| POST | `/v1/videos/{videoId}/view` | Register view |
| POST | `/v1/videos/{videoId}/thumbnail` | Upload thumbnail |

### Comments
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/videos/{videoId}/comments` | Add comment |
| GET | `/v1/videos/{videoId}/comments` | List comments |
| GET | `/v1/comments/{commentId}/replies` | List replies |
| PUT | `/v1/comments/{commentId}` | Edit comment |
| DELETE | `/v1/comments/{commentId}` | Delete comment |
| POST | `/v1/comments/{commentId}/reactions` | React to comment |
| DELETE | `/v1/comments/{commentId}/reactions` | Remove comment reaction |

### Playlists
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/playlists` | Create playlist |
| GET | `/v1/playlists/{playlistId}` | Get playlist |
| PUT | `/v1/playlists/{playlistId}` | Update playlist |
| DELETE | `/v1/playlists/{playlistId}` | Delete playlist |
| GET | `/v1/channels/{channelId}/playlists` | List by channel |
| GET | `/v1/users/{userId}/playlists` | List by user |
| POST | `/v1/playlists/{playlistId}/items` | Add video |
| DELETE | `/v1/playlists/{playlistId}/items/{videoId}` | Remove video |

### Webhooks
| Method | Path | Description |
|--------|------|-------------|
| POST | `/webhooks/minio-upload-completed` | MinIO upload event |
| POST | `/webhooks/video-processed` | Processor finished event |

## Project Structure

```
src/
├── VidroApi.Domain/          # Entities, enums, DomainError
├── VidroApi.Application/     # Interfaces, shared abstractions
├── VidroApi.Infrastructure/  # EF Core, MinIO, Redis, JWT
└── VidroApi.Api/
    ├── Features/             # Vertical slices
    │   ├── Auth/
    │   ├── Channels/
    │   ├── Comments/
    │   ├── Playlists/
    │   ├── Users/
    │   └── Videos/
    ├── BackgroundServices/
    └── Program.cs
tests/
├── VidroApi.UnitTests/
└── VidroApi.IntegrationTests/
```

## Related Projects

- [VidroProcessor](../Processor) — Go worker that processes uploaded videos
- [VidroFront](../FrontNovo) — React/TanStack Start frontend
