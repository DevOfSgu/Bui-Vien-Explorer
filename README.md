# Hệ Thống Du Lịch Ẩm Thực Quận 4 — Bui Vien Explorer

> 🍜 **Full-stack PWA**: Bản đồ tương tác, hướng dẫn âm thanh đa ngôn ngữ, hoạt động hoàn toàn offline — khám phá ẩm thực đường phố Quận 4, TP.HCM

## 🚀 Tổng Quan Nhanh

| | |
|---|---|
| **Backend** | FastAPI + MongoDB + Edge-TTS + Google Gemini 2.0 Flash |
| **Frontend** | React 18 + Vite PWA + MapLibre + PMTiles |
| **Modules** | 6 modules hoạt động độc lập (content, audio, admin, ai_advisor, localization, maps) |
| **Offline** | 4-Tier Hybrid Architecture (Pre-gen → On-demand → Cloud TTS → Local TTS) |
| **Cost** | $0 Core API — zero cost cho Edge-TTS, deep-translator, PMTiles, MapLibre |

---

## 📊 Thống Kê Chính

- **6** hệ thống (modules) hoạt động độc lập
- **~60** admin endpoints
- **29** RBAC permissions / 9 domains
- **4** Audio tiers (hybrid playback)
- **3** Content fallback layers
- **4** Offline defense layers
- **3** Map modes (Cloud / Offline / Hybrid)
- **$0** Core API cost

---

## 📋 Mục Lục

1. [Kiến Trúc Tổng Quan](#kiến-trúc-tổng-quan)
2. [Luồng Khởi Động](#luồng-khởi-động)
3. [GPS + Geofence + Audio Narration](#gps--geofence--audio-narration)
4. [Module Content — POI Lifecycle](#module-content--poi-lifecycle)
5. [Module Audio / TTS](#module-audio--tts)
6. [Authentication & RBAC](#authentication--rbac)
7. [Đa Ngôn Ngữ (Localization)](#đa-ngôn-ngữ-localization)
8. [Offline / PWA Architecture](#offline--pwa-architecture)
9. [Map Pack — PMTiles Offline](#map-pack--pmtiles-offline)
10. [Admin Dashboard + Owner Portal](#admin-dashboard--owner-portal)
11. [Design Patterns & Constants](#design-patterns--constants)
12. [Technology Stack](#technology-stack)

---

## 🏗️ Kiến Trúc Tổng Quan

### Backend Architecture

Backend chia thành **6 module** hoạt động độc lập theo kiểu REST API, giao tiếp qua MongoDB (database `quan4_culinary`). 2 module reserved cho tương lai.

#### 6 Modules Chính

| Icon | Module | Mô Tả |
|------|--------|-------|
| 📍 | **content** | CRUD POI, hình ảnh, cascade delete, load-all API |
| 🎧 | **audio** | Edge-TTS, deep-translator, AudioTaskManager, SSE |
| 🛡️ | **admin** | Auth, RBAC, User/Role CRUD, PII Encryption |
| 🧠 | **ai_advisor** | Gemini 2.0 Flash, rate limit 10/day, prompt engineering |
| 🌐 | **localization** | On-demand, Hotset, Warmup, poi_localizations |
| 🗺️ | **maps** | PMTiles, manifest, fonts, sprites, Path Traversal guard |

#### 2 Modules Reserved
- 📍 **geo** — Routing, server-side geofencing, heatmap
- 💳 **auth_payment** — Voucher, subscription, settlement

### MongoDB Collections

| Collection | Module | Mục Đích |
|---|---|---|
| `pois` | content | Dữ liệu POI gốc (tiếng Việt), hình ảnh, tọa độ, geofence_radius |
| `poi_localizations` | localization | Bản dịch + audio_url theo (poi_id, lang). Compound index. |
| `admin_users` | admin | Tài khoản admin/owner + hashed password + role + PII encrypted |
| `roles` | admin | Dynamic roles + permission arrays. 4 default roles seeded. |
| `audit_logs` | admin | Nhật ký hành động (action, user_id, resource, timestamp) |
| `poi_owner_registrations` | admin | Đơn đăng ký chủ quán (pending → approved/rejected) |
| `poi_submissions` | admin | Bài đăng chờ duyệt từ owner |
| `ai_usage_limits` | ai_advisor | Rate limit: {user_id, date, count}. Reset daily. |
| `menus` | content | Menu items cho POI |

---

## 🚀 Luồng Khởi Động

### Backend Startup Sequence

#### **Tầng 1 — Security Config Check**
Kiểm tra `JWT_SECRET`, `REFRESH_TOKEN_SECRET`, `PII_ENCRYPTION_KEY` đã thay đổi khỏi giá trị default. Từ chối khởi động nếu config không an toàn.

#### **Tầng 2 — Database Connection**
Kết nối MongoDB (`quan4_culinary`) qua Motor async driver. Kiểm tra connectivity.

#### **Tầng 3 — Data Seeding**
`admin_service.ensure_roles()` → tạo 4 default roles + Super Admin account nếu chưa có.

#### **Tầng 4 — Indexing**
- Compound index `{poi_id, lang}` trên `poi_localizations`
- Index `2dsphere` trên `pois.location` cho `$nearSphere` query

#### **Tầng 5 — Router Mounting**
Gắn 7 routers → Server ready: content, audio, admin, owner, ai_advisor, localization, maps — prefix `/api/v1`

### Frontend Startup Flow

```
SW Register → React Router (catch-all) → Splash Screen → Chọn ngôn ngữ → Parallel Load
```

#### **Parallel Loading Tasks**

| Task | Mô Tả | Thời Gian |
|------|-------|----------|
| 🛰️ GPS | `waitForPosition()` — lấy tọa độ lần đầu. High accuracy, timeout 10s. | ~3-10s |
| 💾 Offline Data | `getOfflinePOIs(lang)` — móc IndexedDB, hiện dữ liệu tức thì | 0ms |
| 🌐 Online Data | `loadAllPOIs(lang)` → API call → `savePOIs()` → cập nhật IDB | 2-5s |
| 🔥 Hotset | Quét 1.5km, dịch trước 10 POI gần nhất → user nghe được ngay | 2-5s |
| ♨️ Warmup | `POST /warmup` → server dịch full corpus dưới nền → chuẩn bị offline | ~30s (background) |

---

## 📡 GPS + Geofence + Audio Narration

### 4 Giai Đoạn Xử Lý

#### **Giai Đoạn 1 — GPS Collection (LocationService)**
- `watchPosition()` liên tục → **throttle 5s** → tránh tính Haversine/vẽ WebGL/sort POI liên tục
- Cập nhật chấm xanh trên map

#### **Giai Đoạn 2 — Zone Detection (GeofenceEngine)**
- `checkGeofences(pos)`: `turf.distance()` tính khoảng cách
- ≤ radius (default 30m) → pendingEntries
- Ra ngoài zone đã trigger → cooldown 5 phút

#### **Giai Đoạn 3 — Audio Decision (Heartbeat 1s)**
- `reconcileLoop` mỗi 1s: confirm ENTER sau 3s debounce
- Chọn POI ưu tiên (audio_priority → khoảng cách)
- Đẩy NarrationEngine

#### **Giai Đoạn 4 — Audio Playback (4-Tier Hybrid)**
- NarrationEngine → AudioQueueManager (single-slot priority queue)
- `playWithFallback()` giải quyết ba tầng fallback

### 4-Tier Hybrid Audio

#### **Tier 1 — Pre-generated Audio (0ms)**
- `playAudioUrl(poi.audio_url)` → SW CacheFirst → phát từ cache
- Điều kiện: có `audio_url`, không phải `is_fallback`

#### **Tier 1.5 — On-demand Translate + TTS (2-5s)**
- `POST /localizations/on-demand` → Backend dịch + Edge-TTS → lưu → trả audio_url mới
- Khi POI có `is_fallback=true`

#### **Tier 2 — Cloud TTS Stream (3-8s)**
- `POST /audio/tts` → Edge-TTS synthesis → stream MP3 trực tiếp
- Disk cache cho lần sau

#### **Tier 3 — Local Speech Synthesis (0ms)**
- `window.speechSynthesis` — offline fallback cuối cùng
- Chất lượng thấp hơn nhưng luôn khả dụng

#### **Background Prefetch**
Song song, `_prefetchNearbyLocalizations(pos)` quét dataset POI đang có trong store:
- Lọc POI `is_fallback=true` trong 500m
- Enqueue tối đa 3 POI mỗi đợt rồi drain tuần tự
- Đợt mới chỉ mở sau tối thiểu 30 giây
- Nếu gặp `429` → backoff 30s → 60s → 120s ... (tối đa 10 phút)

---

## 📍 Module Content — POI Lifecycle

### APIs Chính

#### **GET /poi/load-all**
API nặng nhất: tải toàn bộ POI kèm localization
- Backend hydrate `poi_localizations` theo lang
- 3-Tier Content Fallback
- Decorate `audio_url` với `?v={mtime}&l={lang}`

#### **GET /poi/nearby**
- Truy vấn `$nearSphere` (2dsphere index)
- Lấy POI trong bán kính
- Phục vụ Hotset Service

#### **Create POI**
- Lưu text DB (nhanh)
- Background Task: dịch + TTS 5 ngôn ngữ (vi, en, zh, ja, ko)
- `audio_status = "processing" → "completed"`

#### **Delete POI (Cascade)**
- Xóa record
- Xóa tất cả `poi_localizations`
- Dọn file ảnh/MP3 rác trên ổ cứng

### 3-Tier Content Fallback

#### **Tier 1 — Ngôn Ngữ Yêu Cầu**
VD: tiếng Pháp. Nếu thiếu + là English → English Self-Heal (auto dịch + TTS ngay)

#### **Tier 2 — English Fallback**
Ngôn ngữ quốc tế chung. Đánh dấu `is_fallback=true`

#### **Tier 3 — Nguyên Bản Tiếng Việt**
Lớp phòng thủ cuối. `audio_url = null` (ép không phát audio tiếng Việt cho khách nước ngoài)

#### **Runtime Audio Decoration**
Mỗi `audio_url` được gắn `?v={mtime}&l={lang}` trước khi trả frontend:
- Tham số `v` giúp cache-bust khi file thay đổi
- Tham số `l` giúp Service Worker phân loại audio vào đúng language shard

---

## 🎧 Module Audio / TTS

### TTSService — Nhà Máy Audio

- **translate_text()**: deep-translator (GoogleTranslator) — miễn phí
- **generate_audio()**: dịch → Edge-TTS synthesis → lưu MP3
- **Cache key**: `MD5(f"{text}:{lang}")` → file tồn tại = Cache HIT, zero cost
- **5 preferred voices**:
  - Vietnamese: HoaiMyNeural
  - English: JennyNeural
  - Chinese: XiaoxiaoNeural
  - Japanese: NanamiNeural
  - Korean: SunHiNeural

### AudioTaskManager — Background Engine

- Parallel generate với **Semaphore max=3**
- SSE real-time: `GET /admin/audio-tasks/stream`
- Frontend: progress bar + Pause/Resume/Cancel
- Status: queued → running → paused → completed/failed/cancelled

### Audio Generation Pipeline

```
Source Text (VN) → deep-translator → Translated Text → MD5 Hash Check → Edge-TTS → MP3 File → Upsert Localization
```

### Key Endpoints

| Endpoint | Mô Tả |
|----------|-------|
| `POST /audio/tts` | Text → Edge-TTS → stream MP3 trực tiếp. Disk cache: `X-Cache: HIT/MISS` |
| `GET /audio/pack-manifest` | Quét tất cả MP3 cho 1 lang. Return: `{lang, pack_version, total_files, total_bytes, files[]}` |

---

## 🛡️ Authentication & RBAC

### Authentication Method

#### **httpOnly Cookie Auth**
- `access_token`: JWT, 30 phút, httpOnly, SameSite=Lax, Secure
- `refresh_token`: JWT, 7 ngày, httpOnly
- JS không đọc được (chống XSS)
- SameSite chống CSRF
- Dual-mode: Cookie (browser) + Bearer header (API/mobile fallback)

### Dynamic RBAC

- **Static**: 29 permissions / 9 domains (code)
- **Dynamic**: Roles in MongoDB (DB)
- **Route Guards**: `require_permission("poi:delete")`
- **JWT payload** chứa permissions → không cần query DB

### Permission Domains (9)

| Domain | Permissions |
|--------|-------------|
| `poi` | read, create, update, delete, approve, toggle |
| `menu` | read, create, update, delete |
| `user` | read, create, update, delete |
| `role` | read, create, update, delete |
| `analytics` | view, export, view_own |
| `audit` | read, manage |
| `system` | config, logs, backup |
| `owner` | register, access, submit_poi, manage_own_poi |
| `content` | moderate, publish |

### Default Roles

| Role | Priority | Permissions |
|------|----------|-------------|
| **super_admin** | 0 (cao nhất) | TẤT CẢ 29 permissions |
| **admin** | 1 | POI/Menu/User + Analytics + Audit + Content |
| **poi_owner** | 10 | poi:read + owner:* + menu:read/create/update + analytics:view_own |
| **user** | 100 | poi:read + menu:read + owner:register |

### PII Encryption

Số CCCD chủ quán → `encrypt_pii()` = Fernet encryption (`"v1:" + encrypted`) → lưu DB:
- Sau 180 ngày → auto redacted
- Decrypt chỉ khi cần
- Return None on failure (never leak)

---

## 🌐 Đa Ngôn Ngữ (Localization)

### 4 Luồng Hoạt Động

#### **Flow 1 — Bulk Load (Startup)**
- `GET /poi/load-all?lang=ja` → hydrate `poi_localizations`
- 3-Tier Content Fallback → trả bản dịch có sẵn
- Hiển thị nhanh nhất có thể

#### **Flow 2.1 — Hotset (Đón Đầu)**
- Mở app → GPS → tìm 10 POI / 1.5km
- `POST /localizations/prepare-hotset` → dịch + TTS trước
- Wait ≤ 2.5s cho GPS

#### **Flow 2.2 — On-demand (Reactive)**
- User vào zone POI chưa có ngôn ngữ
- `POST /localizations/on-demand` → dịch tức thì (2-3s)
- Rate limit: 30 req/10 phút

#### **Flow 3 — Warmup (Full Sync)**
- `POST /warmup` → server dịch toàn bộ POI dưới nền
- `ready: true` → user tải Audio Pack offline
- Đi chơi cả ngày không cần mạng

### Tại Sao Cần Cả Hotset Lẫn Warmup?

- **Hotset** giải quyết vấn đề "vào app lần đầu với ngôn ngữ mới" — chỉ dịch 10 quán gần nhất, xong trong vài giây
- **Warmup** chạy dưới nền để dịch toàn bộ corpus, chuẩn bị cho offline pack và đảm bảo mọi lần truy cập sau đều Cache HIT

---

## 📴 Offline / PWA Architecture — 4 Lớp Phòng Ngự

### Lớp 1 — Service Worker Strategies

`sw.js` (584 dòng, Workbox-based). Chiến lược tùy biến:

| Resource | Chiến Lược | TTL |
|----------|-----------|-----|
| POI data | NetworkFirst 8s → cache | 15 phút |
| Audio | CacheFirst per-lang | Unlimited |
| Images | CacheFirst | 30 ngày (max 50) |
| Map pack | Custom, immutable | - |

### Lớp 2 — Language Sharding + LRU

Mỗi ngôn ngữ = ngăn kéo riêng. URL param `?l={lang}` → SW phân loại:
- Max 300 files / ngôn ngữ
- Max 3 ngôn ngữ đồng thời
- LRU eviction: xóa lang ít dùng nhất

### Lớp 3 — IndexedDB (Quan4DB v2)

Store `pois_by_lang`, key = `"{lang}:{poiId}"`:
- **Offline Fallback**: target → en → vi
- Bỏ qua `is_fallback=true` trong target lane
- User không bao giờ thấy màn hình trống

### Lớp 4 — Offline Packs

Audio Pack + Map Pack = "container bảo mật":
- SHA-256 verify per file
- Pack cache tách biệt khỏi runtime cache
- SW ưu tiên: Pack → Runtime → Network
- `purgeOnQuotaError: true` → hy sinh runtime khi disk đầy

### SW Message Protocol

| Message | Từ | Mục Đích |
|---------|-----|----------|
| `SET_ACTIVE_LANGUAGE` | Frontend | Ghim bộ nhớ đệm ngôn ngữ đang dùng. Khi hết dung lượng, xóa lang khác nhưng không xóa ngôn ngữ hiện tại |
| `AUDIO_PACK_ACTIVATE` | AudioPackService | Audio pack tải xong → set active cache. Xóa phiên bản gói cũ → giải phóng bộ nhớ |
| `AUDIO_PACK_REMOVE_LANG` | AudioPackService | Xóa audio pack → cleanup cache |
| `MAP_PACK_ACTIVATE` | MapPackService | Map pack install → activate. Chuyển từ bản đồ cũ sang bản mới vừa tải |
| `MAP_PACK_DEACTIVATE` | MapPackService | Xóa map pack → cleanup |

### Edge Case — Disk Full

Khi tải Pack mà hết dung lượng → `QuotaExceededError` → Workbox tự xóa runtime cache + image cache (có `purgeOnQuotaError: true`) để nhường chỗ cho Pack. "Hy sinh cái nhỏ, giữ cái lớn."

---

## 🗺️ Map Pack — PMTiles Offline

Toàn bộ Quận 4 trong 1 file PMTiles. Giao thức `pmtiles://` cho phép MapLibre truy xuất từ bộ nhớ cục bộ tức thời.

### Chuỗi Liên Kết 4 Tầng (End-to-End)

| Tầng | Thành Phần | Vai Trò |
|------|-----------|--------|
| 1. Mode Selection | `mapConfig.js` (Cloud / Offline / Hybrid) | Chọn chiến lược render theo trạng thái mạng |
| 2. Pack Publication | Manifest + endpoints `/maps/*` | Backend công bố phiên bản pack, checksum, assets |
| 3. Pack Activation | Frontend downloader + SHA-256 verify | Tải tài nguyên, xác thực toàn vẹn, activate cache |
| 4. Runtime Rendering | MapLibre + `pmtiles://` | Render từ local cache; khi online có thể ghép Cloud/Hybrid |

### 3 Chế Độ Bản Đồ

#### **☁️ Cloud (Default)**
- MapTiler Streets v4 API
- Cần `VITE_MAPTILER_KEY`
- Dữ liệu toàn cầu, chất lượng cao

#### **📦 Offline Pack**
- PMTiles từ Cache API
- Hoạt động 100% offline
- Chỉ có dữ liệu Quận 4 (bbox: 106.69–106.715, 10.745–10.765)

#### **🔄 Hybrid Q4**
- Cloud base + PMTiles Q4 overlay
- Bản đồ toàn cầu + chi tiết Q4 local
- Tối ưu khi mạng yếu

### Backend Map Service

| Endpoint | Mô Tả | Cache |
|----------|-------|-------|
| `GET /maps/offline-manifest` | Manifest: bbox, checksums (SHA-256), asset URLs | no-cache |
| `GET /maps/packs/{version}/{file}` | Serve PMTiles (Range Requests) | immutable |
| `GET /maps/styles/{path}` | Style JSON + sprites | revalidate / immutable |
| `GET /maps/fonts/{fontstack}/{range}.pbf` | Glyph PBFs | immutable |

#### **Security**
`resolve_safe_path(base, relative)` → mọi đường dẫn phải `resolve()` nằm trong base dir. Chặn hoàn toàn Path Traversal attack (`../../etc/passwd`).
Data dir: `backend/app/static/maps/` (override: `MAP_PACK_DATA_DIR` env)

---

## 👨‍💼 Admin Dashboard + Owner Portal

Platform 3 cổng: Owner (B2B), Admin (vận hành), AI Advisor (nâng cấp nội dung).

### 🏪 Owner Portal

- **Đăng ký**: `POST /admin/auth/register-owner`
- **Admin duyệt** → `is_verified=true`
- **Quản lý POI**: `PUT /owner/pois/{id}` (chỉ quán mình)
- **Submissions** → Admin phê duyệt mới lên app
- **PII Encryption**: CCCD mã hóa Fernet

### 🎛️ Admin Dashboard (~60 APIs)

- **CRUD**: POIs, Users, Roles, Menus
- **Kiểm duyệt**: Registrations, Submissions
- **Auth**: `/admin/auth/me`, `POST /admin/auth/change-password`
- **Giám sát**: Analytics, Audit Logs
- **SSE**: Audio Tasks progress real-time

### 🤖 AI Advisor (Gemini 2.0 Flash)

- **Endpoint**: `POST /ai/enhance-description`
- **Prompt**: KHÔNG bịa, CÓ THỂ thêm tính từ tích cực, 200-300 từ
- **Rate limit**: Owner = 10/ngày (`ai_usage_limits`) | Admin = unlimited
- **Timeout**: 30s

### Owner Registration Flow

```
Chủ quán đăng ký → Tạo user (poi_owner, unverified) → Tạo poi_owner_registrations (pending) → Admin duyệt → verified → Owner login → /owner
```

---

## ⚡ Design Patterns & Key Constants

### Architecture Patterns

| Pattern | Mô Tả | Industry Reference |
|---------|-------|-------------------|
| **Modular Monolith** | 6 module độc lập, giao tiếp qua DB | Shopify modular monolith approach |
| **4-Tier Hybrid Audio** | Pre-gen → On-demand → Cloud TTS → Local TTS | Netflix adaptive streaming philosophy |
| **3-Tier Content Fallback** | Target → English → Vietnamese | ICU locale fallback chain (Unicode CLDR) |
| **Language Sharding** | SW cache per language + LRU eviction | CDN edge cache partitioning |
| **Geofence Reconciliation** | Heartbeat 1s, debounce 3s, cooldown 5min | iOS CLLocationManager patterns |
| **Dynamic RBAC** | Static permissions (code) + Dynamic roles (DB) | AWS IAM policy model |
| **httpOnly Cookie Auth** | XSS-safe + SameSite=Lax CSRF protection | OWASP Session Management guidelines |
| **Offline-First PWA** | SW + IndexedDB + Cache API + PMTiles | Google Workbox best practices |
| **SSE Progress Tracking** | AudioTaskManager → EventSource → UI | GitHub Actions live log streaming |
| **PII Encryption at Rest** | Fernet + 180-day auto-redaction | GDPR data minimization principle |

### Key Configuration Constants

| Constant | Value | Location |
|----------|-------|----------|
| ACCESS_TOKEN_EXPIRE | 30 phút | Backend config |
| REFRESH_TOKEN_EXPIRE | 7 ngày | Backend config |
| ROLE_CACHE_TTL | 300s (5 phút) | admin/service.py |
| VOICE_CATALOG_TTL | 6 giờ | audio/service.py |
| MAX_CONCURRENT_TTS | 3 (Semaphore) | audio/task_manager.py |
| PII_RETENTION_DAYS | 180 ngày | admin/service.py |
| MAX_POI_IMAGES | 8 | content/service.py |
| MAX_IMAGE_SIZE | 5 MB | content/service.py |
| ON_DEMAND_RATE_LIMIT | 30 req / 10 phút | localization/service.py |
| HOTSET_MAX_POI_IDS | 10 | localization/service.py |
| AI_DAILY_LIMIT (Owner) | 10 | ai_advisor/service.py |
| GPS Throttle | 5s | LocationService.js |
| Geofence Debounce | 3s | GeofenceEngine.js |
| Geofence Default Radius | 30m | GeofenceEngine.js |
| Geofence Cooldown | 5 phút | GeofenceEngine.js |
| Heartbeat Interval | 1s | GeofenceEngine.js |
| Prefetch Queue Limit | Top 3 / batch, gate ≥ 30s | useGeofence.js |
| Hotset Nearby Radius | 1500m | LanguageHotsetService.js |
| POI Cache TTL | 15 phút | poiStore.js / sw.js |
| Audio Max Per Lang | 300 entries | sw.js |
| Max Language Caches | 3 | sw.js |
| IndexedDB | Quan4DB v2 | db.js |

---

## 🛠️ Technology Stack

### Backend Technologies

- **FastAPI** — async Python web framework
- **Motor** — async MongoDB driver
- **Edge-TTS** — Microsoft Neural Voices (FREE)
- **deep-translator** — Google Translate wrapper (FREE)
- **Google Gemini 2.0 Flash** — AI enhancement
- **bcrypt** — password hashing
- **PyJWT** — JWT tokens (HS256)
- **cryptography (Fernet)** — PII encryption

### Frontend Technologies

- **React 18** — UI framework
- **Vite** — build tool (PWA plugin)
- **Zustand** — state management
- **MapLibre GL JS** — vector maps
- **Turf.js** — geospatial calculations
- **Workbox** — Service Worker toolkit
- **idb** — IndexedDB wrapper
- **PMTiles** — offline vector tiles

### Zero-Cost Core

- ✅ **Edge-TTS**: 300+ neural voices, miễn phí
- ✅ **deep-translator**: Google Translate free tier
- ✅ **PMTiles**: self-hosted, no tile server needed
- ✅ **MapLibre**: open-source, free forever
- ✅ **Workbox**: Google open-source SW toolkit
- ⚠️ **Gemini AI**: API key needed, 10 uses/day/owner
- ⚠️ **MapTiler**: API key for cloud mode only (optional)

---

## 🔄 End-to-End Flow — Từ Mở App Đến Nghe Audio

### Sequence

1. **User mở app** → SW register
2. **React Router** → catch-all → MapApp → SplashScreen
3. **Frontend hiển thị** → User chọn ngôn ngữ
4. **Parallel Load**:
   - `getOfflinePOIs(lang)` → lấy từ IDB (0ms)
   - `GET /poi/load-all?lang=en` → Backend trả POI + Localizations (3-Tier Fallback)
   - `POST /localizations/prepare-hotset` → dịch 10 POI gần nhất (Hotset)
   - `POST /localizations/warmup` → dịch toàn bộ dưới nền
5. **SplashScreen ẩn** → Map render
6. **Frontend bắt đầu** → `startTracking(watchPosition)`
7. **GPS Position Update** (mỗi 5s):
   - GeofenceEngine → `checkGeofences(turf.distance)`
   - pendingEntries → debounce 3s → confirm ENTER
   - `_processAudioDecision` (priority sort)
   - `queueNarration(bestPOI)` → NarrationEngine
8. **NarrationEngine** → `playWithFallback()`:
   - **Tier 1**: Fetch `audio_url` → SW CacheFirst → phát MP3
   - **Tier 1.5**: Nếu `is_fallback=true` → On-demand → Backend dịch + TTS
   - **Tier 2**: Cloud TTS stream → MP3
   - **Tier 3**: Local TTS → `window.speechSynthesis`
9. **Audio kết thúc** → `onFinished` → close popup

---

## 📦 Cài Đặt & Chạy

### Yêu Cầu

- **Backend**: Python 3.10+, MongoDB, Node.js (for Edge-TTS optional)
- **Frontend**: Node.js 16+, npm/yarn
- **Environment Variables**: JWT secrets, API keys (Gemini, MapTiler)

### Backend Setup

```bash
cd backend
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env
# Edit .env với JWT_SECRET, MONGODB_URL, etc.
python -m uvicorn app.main:app --reload
```

### Frontend Setup

```bash
cd frontend
npm install
cp .env.example .env.local
# Edit .env.local với VITE_API_URL, VITE_MAPTILER_KEY, etc.
npm run dev
```

### Docker Compose

```bash
docker-compose up -d
```

---

## 📝 License

[Your License Here]

---

## 👥 Contributors

Phát triển bởi **DevOfSgu** team

---

**Hệ Thống Du Lịch Ẩm Thực Quận 4**  
Full-stack PWA — FastAPI + React + MapLibre + Edge-TTS + Gemini AI
