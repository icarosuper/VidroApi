# VidroApi

REST API for Vidro, a video platform.
Works alongside [VidroProcessor](https://github.com/icarosuper/video-processor-go) for video processing and [VidroFront](https://github.com/icarosuper/VidroFront) as the frontend.
Built with .NET 10, Clean Architecture + Vertical Slice.

## Stack

- **PostgreSQL** — primary database (EF Core)
- **Redis** — job queue (video processing)
- **MinIO** — object storage (videos, thumbnails, avatars)
- **JWT** — auth (access + refresh tokens)

## Quick Start

```bash
docker-compose up -d postgres redis minio
dotnet run --project src/VidroApi.Api
```

Default: `http://localhost:5000`

## Commands

```bash
dotnet build
dotnet test

# EF Core migrations
dotnet ef migrations add <DescriptionMigration> \
  --project src/VidroApi.Infrastructure \
  --startup-project src/VidroApi.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/VidroApi.Infrastructure \
  --startup-project src/VidroApi.Api
```

## Architecture

```
Domain ← Application ← Infrastructure ← Api
```

Each feature is a single self-contained file under `src/VidroApi.Api/Features/<Domain>/`. Domains: Auth, Users, Channels, Videos, Comments, Playlists.
