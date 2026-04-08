# VideoApi — Design Document

**Data:** 2026-03-26
**Status:** Aprovado
**Stack:** .NET 10, PostgreSQL, Redis, MinIO

---

## Visão Geral

API REST para uma plataforma de vídeos similar ao YouTube. Atua como camada de usuários, metadados e orquestração — delega o processamento de vídeo ao serviço **VideoProcessor** (Go) via Redis + MinIO.

### Responsabilidades da API

- Autenticação e gestão de usuários
- Canais (múltiplos por usuário) e seguidores
- Upload de vídeos (presigned PUT URL → MinIO)
- Enfileiramento de jobs de processamento (Redis)
- Receber callbacks do VideoProcessor (webhook)
- Servir metadados e URLs de acesso aos artefatos (presigned GET URLs)
- Comentários, likes/dislikes e feed

---

## Integração com VideoProcessor

O VideoProcessor (Go) se comunica com a API através de dois mecanismos:

### 1. Redis — Publicação de Jobs

A API publica um job chamando `PublishJob(videoId, callbackUrl)`, que:
- Grava estado inicial `pending` na chave `job:{videoId}` no Redis (TTL: 24h)
- Empurra o `videoId` na fila `PROCESSING_REQUEST_QUEUE`

### 2. Webhook — Notificação de Conclusão

Ao terminar, o VideoProcessor faz `POST {callbackUrl}` com:
```
Header: X-Webhook-Signature: sha256={hmac}
Body: {
  "video_id": "...",
  "status": "done" | "failed",
  "error": "...",
  "artifacts": {
    "video": "processed/{id}_processed",
    "thumbnails": "thumbnails/{id}",
    "audio": "audio/{id}.mp3",
    "preview": "preview/{id}_preview.mp4",
    "hls": "hls/{id}"
  },
  "metadata": {
    "duration": 0.0, "width": 0, "height": 0,
    "video_codec": "", "audio_codec": "",
    "fps": 0.0, "bitrate": 0, "size": 0
  }
}
```

A API valida a assinatura HMAC-SHA256 usando `WEBHOOK_SECRET` (mesma variável configurada no VideoProcessor).

### 3. MinIO — Armazenamento Compartilhado

| Prefixo | Responsável | Conteúdo |
|---|---|---|
| `raw/{videoId}` | API (upload) | Vídeo original do usuário |
| `processed/{videoId}_processed` | VideoProcessor | Vídeo transcodificado |
| `thumbnails/{videoId}/` | VideoProcessor | 5 frames JPG automáticos |
| `thumbnails/{videoId}/custom.jpg` | API (upload) | Thumbnail personalizada pelo dono |
| `audio/{videoId}.mp3` | VideoProcessor | Track de áudio |
| `preview/{videoId}_preview.mp4` | VideoProcessor | Prévia baixa qualidade |
| `hls/{videoId}/` | VideoProcessor | Segmentos HLS + playlist |
| `avatars/{userId}` | API (upload) | Foto de perfil do usuário |
| `avatars/channels/{channelId}` | API (upload) | Foto de perfil do canal |

---

## Arquitetura

**Clean Architecture + Vertical Slice Architecture**

Cada feature (endpoint, job, handler) é um arquivo autocontido com seu próprio request, response, validador e handler. Despacho via MediatR.

```
VideoApi/
├── src/
│   ├── VideoApi.Api/               # Entry point: Program.cs, DI, middleware
│   ├── VideoApi.Domain/            # Entidades, enums, erros de domínio
│   ├── VideoApi.Infrastructure/    # EF Core, Redis, MinIO SDK, configurações
│   └── VideoApi.Application/       # Features (Vertical Slices)
│       ├── Auth/
│       │   ├── Register.cs
│       │   ├── Login.cs
│       │   └── RefreshToken.cs
│       ├── Channels/
│       │   ├── CreateChannel.cs
│       │   ├── GetChannel.cs
│       │   ├── UpdateChannel.cs
│       │   ├── DeleteChannel.cs
│       │   ├── FollowChannel.cs
│       │   ├── UnfollowChannel.cs
│       │   └── ListChannelVideos.cs
│       ├── Videos/
│       │   ├── CreateVideo.cs            # Cria registro + presigned PUT URL
│       │   ├── ConfirmUpload.cs          # Verifica MinIO + publica job Redis
│       │   ├── GetVideo.cs               # Detalhes + presigned GET URLs
│       │   ├── ListFeedVideos.cs         # Vídeos de canais seguidos
│       │   ├── ListTrendingVideos.cs     # Vídeos em alta
│       │   ├── DeleteVideo.cs
│       │   └── VideoProcessedWebhook.cs  # Callback do VideoProcessor
│       ├── Reactions/
│       │   ├── ReactToVideo.cs           # Upsert like/dislike
│       │   └── RemoveReaction.cs
│       └── Comments/
│           ├── AddComment.cs
│           ├── ListComments.cs
│           └── DeleteComment.cs
└── tests/
    ├── VideoApi.UnitTests/
    └── VideoApi.IntegrationTests/
```

### Padrão de um Slice

```csharp
public static class CreateVideo
{
    public record Request(Guid ChannelId, string Title, string Description,
                          List<string> Tags, long FileSizeBytes);
    public record Response(Guid VideoId, string UploadUrl, DateTimeOffset ExpiresAt);

    public class Validator : AbstractValidator<Request> { ... }

    public class Handler(IMinioService minio, IVideoRepository repo,
                         IOptions<VideoSettings> settings)
        : IRequestHandler<Request, Response> { ... }

    public static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/videos", ...)
           .RequireAuthorization();
}
```

---

## Modelo de Dados (PostgreSQL)

### Tabelas

```sql
Users
  Id            uuid PK
  Username      text UNIQUE NOT NULL
  Email         text UNIQUE NOT NULL
  PasswordHash  text NOT NULL
  AvatarPath    text                        -- path no MinIO (avatars/{userId}), nullable
  CreatedAt     timestamptz NOT NULL

Channels
  Id              uuid PK
  UserId          uuid FK → Users NOT NULL
  Name            text NOT NULL
  Description     text
  AvatarPath      text                        -- path no MinIO, nullable
  FollowerCount   bigint DEFAULT 0 NOT NULL
  CreatedAt       timestamptz NOT NULL

ChannelFollowers
  ChannelId   uuid FK → Channels  -- PK composta
  UserId      uuid FK → Users     -- PK composta
  CreatedAt   timestamptz NOT NULL

Videos
  Id              uuid PK
  ChannelId       uuid FK → Channels NOT NULL
  Title           text NOT NULL
  Description     text
  Status          text NOT NULL   -- pending_upload | processing | ready | failed
  Tags            text[]
  LikeCount       bigint DEFAULT 0 NOT NULL
  DislikeCount    bigint DEFAULT 0 NOT NULL
  ViewCount       bigint DEFAULT 0 NOT NULL
  DurationSeconds float
  CreatedAt       timestamptz NOT NULL
  UpdatedAt       timestamptz NOT NULL

VideoArtifacts                       -- populado após processamento
  VideoId               uuid PK FK → Videos
  ProcessedPath         text
  ThumbnailPaths        text[]          -- 5 frames gerados pelo VideoProcessor
  AudioPath             text
  PreviewPath           text
  HlsPath               text
  CustomThumbnailPath   text            -- thumbnail do dono (thumbnails/{id}/custom.jpg), nullable

VideoMetadata                     -- populado após processamento
  VideoId     uuid PK FK → Videos
  Width       int
  Height      int
  Fps         float
  VideoCodec  text
  AudioCodec  text
  Bitrate     bigint
  SizeBytes   bigint

Reactions
  UserId    uuid FK → Users    -- PK composta + UNIQUE
  VideoId   uuid FK → Videos   -- PK composta
  Type      text NOT NULL      -- like | dislike
  CreatedAt timestamptz NOT NULL

Comments
  Id        uuid PK
  VideoId   uuid FK → Videos NOT NULL
  UserId    uuid FK → Users NOT NULL
  Content   text NOT NULL
  CreatedAt timestamptz NOT NULL
```

### Indexes

```sql
-- Channels
CREATE INDEX ON Channels (UserId);

-- ChannelFollowers
CREATE INDEX ON ChannelFollowers (UserId, ChannelId);

-- Videos
CREATE INDEX ON Videos (ChannelId, CreatedAt DESC);
CREATE INDEX ON Videos (Status, CreatedAt DESC);
CREATE INDEX ON Videos (CreatedAt DESC);
CREATE INDEX gin_videos_tags ON Videos USING gin (Tags);
CREATE INDEX gin_videos_fts ON Videos
  USING gin (to_tsvector('portuguese', Title || ' ' || coalesce(Description, '')));

-- Comments
CREATE INDEX ON Comments (VideoId, CreatedAt DESC);
```

### Contadores Desnormalizados

`LikeCount`, `DislikeCount`, `ViewCount` em `Videos` e `FollowerCount` em `Channels` são atualizados atomicamente via `UPDATE ... SET count = count + 1` na mesma transação da operação, evitando `COUNT(*)` em leituras.

---

## Fluxo de Upload de Vídeo

```
1. POST /videos
   Body: { channelId, title, description, tags[], fileSizeBytes }
   → Valida tags <= MaxTagsPerVideo (config)
   → Cria registro no DB (status: pending_upload)
   → Gera presigned PUT URL para raw/{videoId} (TTL: 2h)
   Response: { videoId, uploadUrl, expiresAt }

2. Cliente faz PUT direto no MinIO com o arquivo completo

3. POST /videos/{id}/confirm-upload
   → Verifica que raw/{videoId} existe no MinIO
   → Publica job no Redis: PublishJob(videoId, callbackUrl)
   → Atualiza status → "processing"
   Response: { videoId, status: "processing" }

4. [Assíncrono] VideoProcessor processa e chama o webhook

5. POST /webhooks/video-processed
   → Valida assinatura HMAC-SHA256 (X-Webhook-Signature)
   → Se done:  insere VideoArtifacts + VideoMetadata, status → "ready"
   → Se failed: status → "failed", registra mensagem de erro
   Response: 200 OK
```

**Melhoria futura:** multipart upload com resume via localStorage (S3 Multipart API).

---

## Contratos REST

```
Auth
  POST /auth/register          { username, email, password }
  POST /auth/login             { email, password }  →  { accessToken, refreshToken }
  POST /auth/refresh           { refreshToken }

Channels
  POST   /channels                      Criar canal (autenticado)
  GET    /users/{username}/channels/{handle}  Buscar canal público por handle
  PUT    /channels/{handle}             Atualizar canal (autenticado, dono)
  DELETE /channels/{handle}             Deletar canal (autenticado, dono)
  GET    /users/{username}/channels    Listar canais públicos de um usuário
  POST   /users/{username}/channels/{handle}/follow      Seguir canal
  DELETE /users/{username}/channels/{handle}/follow      Deixar de seguir canal
  POST   /channels/{handle}/avatar      Presigned PUT URL para avatar (autenticado, dono)
  GET    /users/{username}/channels/{handle}/videos      Listar vídeos de um canal (cursor-based)

Videos
  POST   /videos                      Cria registro + presigned PUT URL
  POST   /videos/{id}/confirm-upload  Confirma upload e enfileira processamento
  GET    /videos/{id}                 Detalhes + presigned GET URLs dos artefatos
  GET    /videos/feed                 Vídeos recentes dos canais seguidos (cursor-based)
  GET    /videos/trending             Vídeos em alta (score por views + likes + tempo)
  DELETE /videos/{id}
  POST   /videos/{id}/thumbnail       Presigned PUT URL para thumbnail personalizada

Users
  POST   /users/me/avatar             Presigned PUT URL para foto de perfil

Reactions
  PUT    /videos/{id}/reaction        { type: "like" | "dislike" }  — upsert
  DELETE /videos/{id}/reaction

Comments
  POST   /videos/{id}/comments        { content }
  GET    /videos/{id}/comments        cursor-based
  DELETE /videos/{id}/comments/{cid}

Webhooks
  POST   /webhooks/video-processed    Callback interno do VideoProcessor
```

### Paginação

Cursor-based em todas as listagens:
```
GET /videos/feed?cursor=2026-03-20T10:00:00Z&limit=20
Response: { data: [...], nextCursor: "..." }
```

### Trending Score

```sql
ORDER BY (ViewCount * :viewWeight + LikeCount * :likeWeight)
       / POWER(EXTRACT(EPOCH FROM NOW() - CreatedAt) / 3600.0 + 2, :decayFactor) DESC
```

Pesos configuráveis em `appsettings.json`:
```json
"TrendingSettings": {
  "ViewCountWeight": 1.0,
  "LikeCountWeight": 2.0,
  "TimeDecayFactor": 1.5,
  "WindowHours": 48
}
```

---

## Autenticação

JWT com dois tokens:
- **Access token:** 15 min, enviado em `Authorization: Bearer {token}`
- **Refresh token:** 7 dias, armazenado em `RefreshTokens` no banco, rotacionado a cada uso

---

## Configuração (appsettings.json)

```json
{
  "ConnectionStrings": {
    "Postgres": "...",
    "Redis": "..."
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "",
    "SecretKey": "",
    "BucketName": "videos",
    "UseSSL": false,
    "UploadUrlTtlHours": 2
  },
  "Jwt": {
    "Secret": "",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "VideoSettings": {
    "MaxTagsPerVideo": 10
  },
  "TrendingSettings": {
    "ViewCountWeight": 1.0,
    "LikeCountWeight": 2.0,
    "TimeDecayFactor": 1.5,
    "WindowHours": 48
  },
  "Webhook": {
    "Secret": ""
  }
}
```

---

## Dependências Principais

| Pacote | Uso |
|---|---|
| MediatR | Despacho de features (Vertical Slice) |
| FluentValidation | Validação dentro de cada slice |
| Entity Framework Core + Npgsql | ORM + driver PostgreSQL |
| StackExchange.Redis | Publicar jobs na fila |
| Minio | Presigned URLs + upload/download |
| Microsoft.AspNetCore.Authentication.JwtBearer | JWT |
| BCrypt.Net | Hash de senhas |

---

## Melhorias Futuras

### Upload
- Multipart upload com resume (S3 Multipart API + localStorage no cliente)

### Notificações
- Notificação quando o processamento do próprio vídeo terminar (SSE ou SignalR)
- Notificações de novos vídeos dos canais inscritos (feed de atualizações em tempo real)
  - Tabela `Notifications { Id, UserId, Type, Payload, ReadAt, CreatedAt }`
  - Endpoints: `GET /notifications` (listagem), `PATCH /notifications/{id}/read`, `GET /notifications/unread-count`
  - Geradas no webhook handler quando um vídeo fica `ready`, para todos os seguidores do canal

### Busca
- Busca de vídeos por título/descrição/tags via `GET /videos/search?q=...`
  - v1: usar o índice `tsvector` já planejado no PostgreSQL (GIN index em `Title + Description`)
  - v2 (escala): migrar para Elasticsearch quando o Postgres não aguentar

### Operacional
- Dashboard Grafana consumindo métricas do VideoProcessor
- Rate limiting por usuário nos endpoints de upload
