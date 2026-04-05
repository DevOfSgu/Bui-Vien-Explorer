# Bui Vien Explorer — Travel Guide System

> 🧭 **Hệ thống hướng dẫn du lịch & địa điểm thú vị (POI)** cho phố đi bộ bùi viện, TP.HCM. Ứng dụng mobile hỗ trợ GPS, geofencing, phát âm thanh hướng dẫn, yêu thích offline với backend web admin.

## 🚀 Tổng Quan Nhanh

| | |
|---|---|
| **Backend** | ASP.NET Core 10 + SQL Server + Entity Framework Core |
| **Mobile** | .NET MAUI (Android, iOS, macOS, Windows) |
| **Shared** | Shared data models + SQLite factory |
| **Auth** | Cookie-based (Admin + Vendor dual-scheme) |
| **Database** | SQL Server (backend) + SQLite (mobile offline) |
| **Key Features** | GPS tracking, Geofence triggers, TTS narration, Offline favorites, Analytics |

### Chạy Web + SQL bằng Docker (Azure Translator/TTS)

1. Tạo file `.env` từ `.env.example`.
2. Điền các biến Azure trong `.env`.
3. Chạy lệnh:

```bash
docker compose up -d --build
```

4. Mở web tại `http://localhost:5281`.

Biến bắt buộc cần điền trong `.env`:
- `AZURE_TRANSLATOR_KEY`
- `AZURE_TRANSLATOR_REGION`
- `AZURE_SPEECH_KEY`
- `AZURE_SPEECH_REGION`

Biến tùy chọn:
- `AZURE_TRANSLATOR_ENDPOINT` (mặc định: `https://api.cognitive.microsofttranslator.com`)
- `SQL_SA_PASSWORD` (mặc định: `Admin@12345`)

---

## 📊 Thống Kê Chính

- **3 Projects**: Backend Web, Mobile App, Shared Library
- **5 Core Controllers**: Routes, Zones (POI), Narrations, Favorites, Home
- **2 Admin Areas**: `/Admin` (Admin Dashboard), `/Vendor` (Vendor Portal)
- **4 Mobile Pages**: MainPage, LanguageSelection, SavedPage (Favorites), SettingsPage
- **SQLite Storage**: Offline-first approach with local cache sync
- **Targeting Platforms**: Android, iOS, macOS, Windows (MAUI support)

---

## 📋 Mục Lục

1. [Kiến Trúc Tổng Quan](#kiến-trúc-tổng-quan)
2. [Cấu Trúc Projects](#cấu-trúc-projects)
3. [Database Schema](#database-schema)
4. [Mobile App Workflow](#mobile-app-workflow)
5. [GPS & Geofencing](#gps--geofencing)
6. [Audio Narration](#audio-narration)
7. [Offline-First Architecture](#offline-first-architecture)
8. [Admin Dashboard](#admin-dashboard)
9. [API Endpoints](#api-endpoints)
10. [Technology Stack](#technology-stack)

---

## 🏗️ Kiến Trúc Tổng Quan

### Backend Architecture

Kiến trúc **3-tier** với ASP.NET Core:
- **Frontend**: ASP.NET Core MVC Views (Admin + Vendor portals)
- **Business Logic**: Controllers + Services
- **Data Layer**: Entity Framework Core + DbContext
- **Database**: SQL Server (quan4_culinary)

### Projects Structure

```
TravelSystem.sln
├── TravelSystem.Web/                 (ASP.NET Core 10 Web API)
│   ├── Controllers/
│   │   ├── RoutesController          (GET zones by route)
│   │   ├── ZonesController           (POI CRUD)
│   │   ├── NarrationsController      (Audio guiding text)
│   │   ├── FavoritesController       (User saved places)
│   │   └── HomeController            (Landing page)
│   ├── Areas/
│   │   ├── Admin/                    (Admin dashboard)
│   │   └── Vendor/                   (Vendor portal)
│   ├── Data/
│   │   └── AppDbContext.cs           (EF Core DbContext)
│   └── DbScripts/
│       ├── BuiVienExplorerDb.sql     (Production schema)
│       ├── SampleData.sql            (Test data)
│       └── Local_*.sql               (Local dev data)
│
├── TravelSystem.Mobile/              (.NET MAUI Multi-platform App)
│   ├── Services/
│   │   ├── DatabaseService.cs        (SQLite CRUD, local cache)
│   │   ├── ApiService.cs             (HTTP calls to backend)
│   │   └── ApiConstants.cs           (Base URL, endpoints)
│   ├── ViewModels/
│   │   ├── MainPageViewModel.cs
│   │   └── LanguageSelectionViewModel.cs
│   ├── Views/
│   │   ├── MainPage.xaml
│   │   ├── LanguageSelectionPage.xaml
│   │   ├── SavedPage.xaml            (Favorites)
│   │   └── SettingsPage.xaml
│   ├── MauiProgram.cs                (DI setup + fonts)
│   └── Resources/
│       ├── Fonts/                    (Space Grotesk)
│       ├── Images/
│       └── Raw/
│
└── TravelSystem.Shared/              (Shared Models + Factories)
    ├── Models/
    │   ├── LocalZone.cs              (POI entity)
    │   ├── LocalRoute.cs             (Routes)
    │   ├── LocalNarration.cs         (Audio text)
    │   ├── LocalAnalytics.cs         (User activity log)
    │   ├── AppSetting.cs             (Key-value settings)
    │   ├── GuestFavorite.cs          (Saved places)
    │   └── ...
    └── Factories/
        └── SqliteConnectionFactory.cs (Offline DB connection)
```

---

## 🗄️ Database Schema

### Core Entities — SQL Server (Backend)

#### **Zone** — Point of Interest (POI)
```sql
CREATE TABLE Zones (
    Id UUID PRIMARY KEY,
    RouteId UUID NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    Latitude DECIMAL(9,6) NOT NULL,
    Longitude DECIMAL(9,6) NOT NULL,
    GeofenceRadius DECIMAL(5,2) DEFAULT 30.0,
    DisplayOrder INT,
    CreatedAt DATETIME,
    UpdatedAt DATETIME,
    FOREIGN KEY (RouteId) REFERENCES Routes(Id)
);
```

| Trường | Tác Dụng |
|--------|---------|
| `Id` | Unique UUID cho mỗi POI |
| `RouteId` | Link đến tuyến du lịch |
| `Latitude, Longitude` | Tọa độ GPS |
| `GeofenceRadius` | Bán kính trigger hình cầu (mét, mặc định 30m) |
| `DisplayOrder` | Thứ tự hiển thị trên map/danh sách |

#### **Route** — Tuyến du lịch
```sql
CREATE TABLE Routes (
    Id UUID PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    CreatedAt DATETIME,
    UpdatedAt DATETIME
);
```

#### **Narration** — Nội dung hướng dẫn âm thanh
```sql
CREATE TABLE Narrations (
    Id UUID PRIMARY KEY,
    ZoneId UUID NOT NULL,
    Language NVARCHAR(10),         -- 'vi', 'en', 'fr', etc.
    Text NVARCHAR(MAX) NOT NULL,
    AudioUrl NVARCHAR(512),        -- URL tệp MP3 (nếu có)
    AudioStatus NVARCHAR(50),      -- 'pending', 'processing', 'ready', 'failed'
    CreatedAt DATETIME,
    UpdatedAt DATETIME,
    FOREIGN KEY (ZoneId) REFERENCES Zones(Id)
);
```

#### **Analytics** — Ghi nhật ký hoạt động
```sql
CREATE TABLE Analytics (
    Id UUID PRIMARY KEY,
    UserId NVARCHAR(255),
    ZoneId UUID NOT NULL,
    ActionType NVARCHAR(50),       -- 'entered', 'listened', 'saved'
    Timestamp DATETIME,
    DurationSeconds INT,
    FOREIGN KEY (ZoneId) REFERENCES Zones(Id)
);
```

### Mobile Local Storage — SQLite (Offline)

Mobile sử dụng **SQLite** để cache data offline:
- `LocalZone` → tương ứng `Zone` (copy từ SQL Server)
- `LocalRoute` → tương ứng `Route`
- `LocalNarration` → tương ứng `Narration`
- `LocalAnalytics` → tương ứng `Analytics`
- `LocalFavorite` ⭐ — Các POI đã lưu/yêu thích (riêng mobile)
- `AppSetting` — Key-value storage (language, settings)

---

## 📱 Mobile App Workflow

### Startup Flow (4 giai đoạn)

#### **Giai Đoạn 1 — App Initialize**
```csharp
// MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    // 1. Setup DI containers
    builder.Services.AddSingleton<DatabaseService>();
    builder.Services.AddSingleton<ApiService>();
    
    // 2. Register pages & viewmodels
    builder.Services.AddSingleton<MainPage>();
    builder.Services.AddSingleton<LanguageSelectionPage>();
    builder.Services.AddSingleton<SavedPage>();
    builder.Services.AddSingleton<SettingsPage>();
    
    return builder.Build();
}
```

**Tác dụng**: 
- Khởi tạo connection factory SQLite
- Verify schema (create tables if not exist)
- Load app settings

#### **Giai Đoạn 2 — Language Selection**
```
App start → Check AppSetting['SelectedLanguage']
  ├─ Nếu NULL → Show LanguageSelectionPage
  │  (User chọn: Vietnamese, English, French, ...)
  │  → Save to AppSetting['SelectedLanguage']
  │
  └─ Nếu có → Skip, go to MainPage
```

#### **Giai Đoạn 3 — Data Sync (Parallel)**

```
App startup
  ├─ getOfflineZones() → Load từ SQLite (0ms)
  │  └─ Display nếu có cached data
  │
  ├─ API GET /api/zones → Fetch từ backend
  │  └─ Update SQLite local cache
  │
  ├─ requestLocationPermission() → Ask GPS access
  │  └─ startLocationTracking() nếu granted
  │
  └─ loadNarrationsByLanguage() → Tải script theo ngôn ngữ chọn
```

#### **Giai Đoạn 4 — Main Screen Ready**
- Map renders với tất cả POI (zones)
- GPS location marker hiển thị vị trí user
- Geofence monitoring xong → app ready phát audio

### Key Mobile Services

#### **DatabaseService** — Offline Data Management
```csharp
public class DatabaseService
{
    private SqliteAsyncConnection _connection;
    
    // CRUD for LocalZone, LocalRoute, LocalNarration, LocalFavorite
    public async Task<List<LocalZone>> GetZonesAsync()
    public async Task<List<LocalZone>> GetZonesByRouteAsync(string routeId)
    public async Task SaveZonesAsync(List<LocalZone> zones)
    public async Task<List<LocalFavorite>> GetFavoritesAsync()
    public async Task AddFavoriteAsync(LocalZone zone)
    public async Task RemoveFavoriteAsync(string zoneId)
    
    // Settings
    public async Task<string> GetSettingAsync(string key)
    public async Task SetSettingAsync(string key, string value)
}
```

#### **ApiService** — Backend Communication
```csharp
public class ApiService
{
    private static readonly string BaseUrl = ApiConstants.BaseUrl;
    private HttpClient _client;
    
    public async Task<List<Route>> GetRoutesAsync()
    public async Task<List<Zone>> GetZonesByRouteAsync(string routeId)
    public async Task<List<Narration>> GetNarrationsAsync(string zoneId, string language)
    public async Task PostAnalyticsAsync(Analytics log)
}
```

---

## 📡 GPS & Geofencing

### LocationService (Planned)

**Tác dụng**: Liên tục theo dõi vị trí user, tối ưu pin, debounce nhiễu GPS.

```csharp
// Pseudo-code
public class LocationService
{
    private Location _currentLocation;
    private Timer _locationThrottleTimer;
    
    public async Task StartTrackingAsync()
    {
        // Request permissions
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        
        // Watch position with 5s throttle
        var request = new GeolocationListeningRequest(
            accuracy: GeolocationAccuracy.Best,
            timeout: TimeSpan.FromSeconds(5)
        );
        
        _locationToken = await Geolocation.StartListeningAsync(request, OnLocationChanged);
    }
    
    private void OnLocationChanged(Location location)
    {
        _currentLocation = location;
        // Trigger geofence check every 5s (throttled)
        OnLocationUpdated?.Invoke(location);
    }
    
    public Location GetCurrentLocation() => _currentLocation;
}
```

### GeofenceService (Planned)

**Tác dụng**: Kiểm tra vị trí user có vào zone hay không, debounce + cooldown.

```csharp
// Pseudo-code
public class GeofenceService
{
    private HashSet<string> _triggeredZones = new();  // Tránh trigger lặp
    private Dictionary<string, DateTime> _cooldowns = new();  // Cooldown 5 min
    
    public async Task CheckGeofencesAsync(Location userLocation, List<LocalZone> zones)
    {
        foreach (var zone in zones)
        {
            double distance = CalculateHaversine(
                userLocation.Latitude, userLocation.Longitude,
                zone.Latitude, zone.Longitude
            );
            
            bool isInZone = distance <= zone.GeofenceRadius;
            
            if (isInZone && !_triggeredZones.Contains(zone.Id))
            {
                // Debounce 3 seconds — confirm user is actually here
                await Task.Delay(3000);
                
                // Check cooldown (5 min)
                if (!_cooldowns.TryGetValue(zone.Id, out var lastTime) ||
                    DateTime.Now.Subtract(lastTime).TotalMinutes >= 5)
                {
                    _triggeredZones.Add(zone.Id);
                    _cooldowns[zone.Id] = DateTime.Now;
                    
                    // Trigger narration
                    OnZoneEntered?.Invoke(zone);
                }
            }
            else if (!isInZone && _triggeredZones.Contains(zone.Id))
            {
                _triggeredZones.Remove(zone.Id);
                OnZoneExited?.Invoke(zone);
            }
        }
    }
    
    private double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Asin(Math.Sqrt(a));
        return R * c; // Distance in meters
    }
}
```

---

## 🎧 Audio Narration

### NarrationService (Planned)

**Tác dụng**: Phát âm thanh hướng dẫn khi user vào zone.

#### **2-Tier Audio Strategy**

| Tier | Source | Latency | Điều kiện |
|------|--------|---------|----------|
| **Tier 1** | Pre-generated MP3 (`AudioUrl`) | ~200ms | `AudioStatus == "ready"` |
| **Tier 2** | Local TTS (`TextToSpeech`) | ~1-3s | Fallback nếu không có file |

```csharp
// Pseudo-code
public class NarrationService
{
    private TextToSpeech _tts;
    private MediaElement _audioPlayer;
    private Queue<(LocalZone zone, Narration narration)> _narrationQueue = new();
    
    public async Task PlayNarrationAsync(LocalZone zone)
    {
        var narration = await _db.GetNarrationAsync(zone.Id, _selectedLanguage);
        
        if (narration == null)
        {
            // Fallback to Vietnamese
            narration = await _db.GetNarrationAsync(zone.Id, "vi");
        }
        
        try
        {
            // Tier 1: Pre-generated audio
            if (!string.IsNullOrEmpty(narration.AudioUrl) && 
                narration.AudioStatus == "ready")
            {
                var stream = await _http.GetStreamAsync(narration.AudioUrl);
                await _audioPlayer.LoadAsync(stream);
                _audioPlayer.Play();
                OnNarrationStarted?.Invoke(zone);
            }
            else
            {
                // Tier 2: Local TTS
                await _tts.SpeakAsync(narration.Text);
                OnNarrationStarted?.Invoke(zone);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] PlayNarrationAsync Error: {ex.Message}");
        }
    }
    
    private void OnAudioCompleted()
    {
        OnNarrationCompleted?.Invoke();
    }
}
```

---

## 📴 Offline-First Architecture

### 4 Lớp Phòng Ngự

#### **Lớp 1 — Local SQLite Cache**
Mỗi lần app mở:
- Check `LocalZone` table (có data → hiển thị ngay 0ms)
- Nếu không → show "Loading..." placeholder

```csharp
// On app startup
var zones = await _db.GetZonesAsync();
if (zones.Count > 0)
{
    DisplayZones(zones);  // Instant
}
else
{
    ShowLoadingIndicator();
    var backendZones = await _api.GetZonesAsync();
    await _db.SaveZonesAsync(backendZones);  // Cache for next time
    DisplayZones(backendZones);
}
```

#### **Lớp 2 — Automatic Sync (Background)**
Khi có mạng:
- Kiểm tra `LastSyncTime` từ `AppSetting`
- Nếu quá 6 giờ → Auto-sync toàn bộ data
- Nếu lỗi mạng → retry với exponential backoff (30s → 60s → 120s)

```csharp
private async Task AutoSyncAsync()
{
    var lastSync = await _db.GetSettingAsync("LastSyncTime");
    var now = DateTime.UtcNow;
    
    if (lastSync == null || 
        (now - DateTime.Parse(lastSync)).TotalHours > 6)
    {
        try
        {
            var zones = await _api.GetZonesAsync();
            await _db.SaveZonesAsync(zones);
            await _db.SetSettingAsync("LastSyncTime", now.ToIso8601String());
        }
        catch
        {
            // Retry logic with backoff
        }
    }
}
```

#### **Lớp 3 — Fallback Languages**
Nếu user chọn ngôn ngữ không có:
- Cố cấp English script
- Nếu English không có → cấp Vietnamese gốc
- Nếu vẫn không → null → user chỉ nghe TTS

```csharp
public async Task<Narration> GetNarrationWithFallbackAsync(string zoneId, string language)
{
    // Tier 1: Exact language
    var narration = await _db.GetNarrationAsync(zoneId, language);
    if (narration != null) return narration;
    
    // Tier 2: English
    narration = await _db.GetNarrationAsync(zoneId, "en");
    if (narration != null) return narration;
    
    // Tier 3: Vietnamese (default)
    narration = await _db.GetNarrationAsync(zoneId, "vi");
    return narration;  // Có thể null
}
```

#### **Lớp 4 — Downloaded Audio Packs (Future)**
- User có thể tải từng language pack (~50MB mỗi lang)
  - `Settings` → `Download Audio Pack` → `Choose Language`
  - Lưu `.mp3` files vào `FileSystem.Current.AppDataDirectory`
- App kiểm tra local file trước khi fetch từ API

---

## 🎛️ Admin Dashboard

### Dashboard Features (ASP.NET Core Web)

#### **Admin Area**
- **Routes Management**: Create/Edit/Delete routes
- **Zones (POI) Management**: Pin point, set geofence radius, reorder
- **Narrations**: Upload/edit audio scripts, upload MP3 files
- **Analytics Dashboard**: 
  - Top 10 most-heard zones
  - User engagement charts
  - Heatmap of user locations (from Analytics table)
- **User Management**: View/manage registered users

#### **Vendor Area (Chủ Quán/Business Owner)**
- **My Zones**: Edit description only (không được xóa/thêm)
- **My Narrations**: Edit text, request re-generate audio
- **Analytics**: View stats for their own zones only

#### **Authentication**
```csharp
// Program.cs setup
builder.Services.AddAuthentication()
    .AddCookie("AdminAuth", options => 
    {
        options.LoginPath = "/Admin/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddCookie("VendorAuth", options =>
    {
        options.LoginPath = "/Vendor/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
```

---

## 🔌 API Endpoints

### Zones (POI) APIs

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| GET | `/api/zones` | Danh sách tất cả POI |
| GET | `/api/zones/{id}` | Chi tiết 1 POI |
| GET | `/api/routes/{routeId}/zones` | Danh sách POI theo tuyến |
| POST | `/api/zones` | Tạo POI (Admin only) |
| PUT | `/api/zones/{id}` | Cập nhật POI |
| DELETE | `/api/zones/{id}` | Xóa POI |

### Routes APIs

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| GET | `/api/routes` | Danh sách tuyến |
| GET | `/api/routes/{id}` | Chi tiết tuyến |
| POST | `/api/routes` | Tạo tuyến (Admin only) |
| PUT | `/api/routes/{id}` | Cập nhật tuyến |
| DELETE | `/api/routes/{id}` | Xóa tuyến |

### Narrations APIs

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| GET | `/api/narrations?zoneId={id}&language=en` | Lấy script narration |
| POST | `/api/narrations` | Tạo/cập nhật narration |
| POST | `/api/narrations/{id}/generate-audio` | Generate MP3 từ text (TTS) |
| GET | `/api/narrations/{id}/audio` | Download MP3 file |

### Analytics APIs

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| GET | `/api/analytics` | Dashboard stats |
| POST | `/api/analytics` | Mobile submit activity log |
| GET | `/api/analytics/heatmap` | Geo heatmap data |

### Favorites APIs (Mobile)

| Method | Endpoint | Mô Tả |
|--------|----------|-------|
| GET | `/api/favorites/sync?language=en` | Sync favorite changes |
| POST | `/api/favorites` | Add favorite |
| DELETE | `/api/favorites/{zoneId}` | Remove favorite |

---

## 🛠️ Technology Stack

### Backend

- **ASP.NET Core 10.0** — Web framework
- **Entity Framework Core 10.0.3** — ORM
- **SQL Server** — Production database
- **Entity Framework Design** — Migrations & scaffolding

### Mobile

- **.NET MAUI** — Cross-platform UI (Android, iOS, macOS, Windows)
- **MAUI Community Toolkit** (for advanced controls)
- **SQLite (sqlite-net-pcl)** — Local offline database
- **HttpClient** — API calls

### Shared

- **Newtonsoft.Json** — JSON serialization
- **System.ComponentModel.DataAnnotations** — Validation

### Design & Resources

- **Font**: Space Grotesk (5 weights)
- **Platform-specific**: Android, iOS, macOS, Windows folders

---

## 📦 Cài Đặt & Chạy

### Prerequisites

- Visual Studio 2023+ hoặc VS Code + .NET SDK 10.0+
- SQL Server (local hoặc remote)
- Android SDK / iOS SDK (for mobile development)

### Backend Setup

```bash
# Restore dependencies
dotnet restore

# Create database from SQL script
# src/TravelSystem.Web/DbScripts/BuiVienExplorerDb.sql
# Load vào SQL Server

# Run migrations (if using EF migrations)
dotnet ef database update

# Start backend
cd src/TravelSystem.Web
dotnet run
# Backend available at https://localhost:5001
```

### Mobile Setup

```bash
# Build for Android
cd src/TravelSystem.Mobile
dotnet publish -f net10.0-android -c Release

# Build for iOS
dotnet publish -f net10.0-ios -c Release

# Debug on emulator
dotnet build -f net10.0-android -t Run
```

### Docker Compose

```bash
docker-compose up -d
# Services: SQL Server, Backend Web, optionally Mobile emulator
```

---

## ✅ Project Checklist

Xem [**CHECKLIST.md**](./CHECKLIST.md) để biết danh sách features đang phát triển:

🔴 **Ưu tiên cao**:
1. LocalFavorite model + DatabaseService
2. LocationService (GPS foreground + background)
3. GeofenceService (Haversine + debounce)
4. NarrationService (TTS + audio queue)

🟡 **Ưu tiên trung**:
5. AppShell TabBar + navigation
6. MapPage
7. PoiListPage + PoiDetailPage

🟢 **Ưu tiên thấp**:
8. FavoritesPage
9. SettingsPage
10. Analytics dashboard + heatmap

---

## 📝 License

[Specify your license here]

---

## 👥 Contributors

Developed by **DevOfSgu** team

---

**Bui Vien Explorer**  
Travel Guide System for BUI VIEN WALK STREET, HCMC  
ASP.NET Core + .NET MAUI + SQL Server
