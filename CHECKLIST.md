# 📋 CHECKLIST — smart-tts-guide (Bùi Viện Explorer)

> Cập nhật lần cuối: tháng 6/2025
> Đánh dấu hoàn thành: đổi `[ ]` thành `[x]`

---

## 🗄️ TravelSystem.Shared

- [x] `LocalZone.cs` — POI model (lat/lng, radius, priority)
- [x] `LocalRoute.cs` — Route model
- [x] `LocalNarration.cs` — Narration model
- [x] `LocalAnalytics.cs` — Analytics model
- [x] `AppSetting.cs` — Key-value settings
- [x] `SqliteConnectionFactory.cs`
- [ ] `LocalFavorite.cs` — Model lưu địa điểm yêu thích offline

---

## 📱 TravelSystem.Mobile

### 🏗️ Nền tảng & Setup

- [x] `MauiProgram.cs` — DI, fonts, services
- [x] `App.xaml.cs` — Khởi tạo DB khi app start
- [x] `AppShell.xaml.cs` — Navigation, kiểm tra onboarding
- [x] `DatabaseService.cs` — SQLite CRUD, Settings
- [x] `ApiService.cs` — HTTP client cơ bản
- [x] `ApiConstants.cs` — Base URL
- [x] `AppLogger.cs` — Debug log (Debug.WriteLine wrapper)
- [ ] Cập nhật `AppShell.xaml` — Thêm **TabBar** (Map / Danh sách / Yêu thích / Cài đặt)
- [ ] Cập nhật `DatabaseService.cs` — Đăng ký bảng `LocalFavorite`
- [ ] Xin quyền GPS khi app mở lần đầu (Permissions)
- [ ] Sync API → SQLite — Tải dữ liệu Zone/Route/Narration về local khi có mạng

### 📍 Services cốt lõi

- [ ] `LocationService.cs` — GPS tracking foreground + background, tối ưu pin
- [ ] `GeofenceService.cs` — Haversine distance, trigger theo ngưỡng, debounce + cooldown
- [ ] `NarrationService.cs` — TTS bản địa, Audio file player, hàng chờ (queue), chống trùng lặp, tự dừng khi có notification

### 🖼️ Views & ViewModels

- [x] `LanguageSelectionPage` — Chọn ngôn ngữ lần đầu
- [x] `MainPage` — Trang chủ tạm (demo)
- [ ] `MapPage` — Bản đồ, vị trí user, tất cả POI, highlight POI gần nhất, tap xem chi tiết
- [ ] `PoiListPage` — Danh sách POI theo route, tìm kiếm
- [ ] `PoiDetailPage` — Chi tiết POI: ảnh, mô tả, nút phát audio, nút yêu thích
- [ ] `FavoritesPage` — Danh sách POI đã lưu ❤️, xóa khỏi yêu thích
- [ ] `SettingsPage` — Bán kính geofence, giọng TTS, ngôn ngữ, tải gói offline

### ⚙️ Tính năng phụ

- [ ] Offline indicator — Thông báo khi mất mạng
- [ ] Download audio offline — Tải file audio về máy để dùng không cần mạng
- [ ] Ghi `LocalAnalytics` — Log mỗi lần phát audio (ZoneId, thời điểm, thời lượng)

---

## 🌐 TravelSystem.Web

### ✅ API đã có

- [x] `GET /api/routes` — Danh sách tuyến
- [x] `GET /api/zones?routeId=` — POI theo tuyến
- [x] `GET /api/narrations` — Nội dung thuyết minh

### 🔲 API còn thiếu

- [ ] `GET /api/zones/sync?updatedAfter=` — Sync delta (chỉ lấy dữ liệu mới hơn timestamp)
- [ ] `POST /api/analytics` — Mobile gửi log nghe lên server

### 🖥️ Web Admin

- [x] Controller: Dashboard, Users, Zones, Narrations, Routes, VisitLogs, Settings, Auth
- [x] Controller: Heatmap
- [ ] Kiểm tra Views `.cshtml` — đảm bảo đầy đủ cho tất cả controller
- [ ] Trang Analytics — Biểu đồ Top POI được nghe nhiều nhất
- [ ] Trang Analytics — Thời gian nghe trung bình mỗi POI
- [ ] Heatmap UI — Hiển thị bản đồ nhiệt vị trí người dùng
- [ ] Upload audio file — Cho phép admin upload file `.mp3` cho từng POI

---

## 🎯 Thứ tự ưu tiên

```
Ưu tiên 1 🔴  LocalFavorite model + DatabaseService fix
Ưu tiên 2 🔴  LocationService (GPS foreground + background)
Ưu tiên 3 🔴  GeofenceService (Haversine + cooldown)
Ưu tiên 4 🔴  NarrationService (TTS + audio queue)
Ưu tiên 5 🟡  AppShell TabBar + navigation structure
Ưu tiên 6 🟡  MapPage
Ưu tiên 7 🟡  PoiListPage + PoiDetailPage
Ưu tiên 8 🟢  FavoritesPage
Ưu tiên 9 🟢  SettingsPage
Ưu tiên 10 🟢 API analytics endpoint + Web Admin analytics UI
```

---

## 📝 Ghi chú

- **Geofencing**: Dùng Haversine formula thay vì Native API để tương thích cả Android + iOS
- **TTS**: Ưu tiên TTS bản địa (nhẹ, offline), File Audio thu sẵn cho POI quan trọng
- **Offline-first**: SQLite local là source of truth, sync từ server khi có mạng
- **Khu vực**: Phường Khánh Hội, Vĩnh Hội, Xóm Chiều — đường Bùi Viện
- **Analytics**: Gửi lên server ở dạng ẩn danh, không cần tài khoản
