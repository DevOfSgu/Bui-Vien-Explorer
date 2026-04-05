# TravelSystem Mobile - Bùi Viện Explorer

> Tài liệu tóm tắt PRD để đối chiếu và triển khai.

- Sản phẩm: TravelSystem Mobile - Bùi Viện Explorer (hệ thống hướng dẫn du lịch theo tour)
- Nền tảng: .NET MAUI (Mobile), ASP.NET Core 10 (Web Admin/Vendor)
- Cập nhật lần cuối: 03/2026
- Trạng thái: Được phê duyệt

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

## 1. Tổng Quan Sản Phẩm

TravelSystem Mobile là ứng dụng dẫn đường du lịch theo hành trình, tối ưu cho môi trường đi bộ phức tạp như phố Tây Bùi Viện.

Mục tiêu trải nghiệm cốt lõi:
- Khám phá Tour
- Điều hướng tại chỗ
- Lưu điểm quan tâm
- Quay lại sau

Điểm khác biệt cốt lõi:
- Xử lý GPS nâng cao: lọc nhiễu, chống nhảy vị trí (Hysteresis), debounce xác nhận vào zone.
- Hoạt động mượt mà dựa trên cơ chế Offline-First: Nạp trước dữ liệu Tour và Bản vẽ bản đồ vào SQLite, cho phép hiển thị tuyến đường, POI và thực hiện thuật toán GPS mà không cần kết nối Internet.
- Anonymous Session: bảo vệ danh tính người dùng, không bắt buộc đăng ký.

---

## 2. Chân Dung Người Dùng

- Du khách quốc tế (Backpacker): cần bản đồ offline, audio đa ngôn ngữ, trải nghiệm đơn giản không đăng ký.
- Giới trẻ khám phá: cần tour ẩm thực/pub crawl, lưu nhanh điểm có vibe tốt, xem giờ mở cửa thực tế.
- Chủ hộ kinh doanh (Vendor): cần cập nhật thông tin quán và giờ mở cửa chính xác.
- Admin hệ thống: quản lý Tour (Routes) và POI (Zones) thuộc từng tour.

---

## 3. User Stories

### 3.1 Onboarding & Home

- US-01: Người dùng mới được cấp Anonymous Session ngay khi mở app.
- US-02: Pre-warm quyền vị trí và đồng bộ dữ liệu ngầm ở màn hình chờ để vào tour không trễ.
- US-03: Hiển thị danh sách Tours ở Home để chọn tour phù hợp.

### 3.2 Navigation & Map

- US-04: Thấy vị trí thực tế di chuyển trên tuyến đường Tour (Route), đồng thời thấy rõ vùng bán kính (Radius Circle) của các POI để biết lúc nào sẽ 'chạm' vùng kích hoạt Audio.
- US-05: Auto-focus POI gần nhất ổn định khi đứng giữa 2 điểm sát nhau.

### 3.3 POI Detail & Saved

- US-06: Xem POI detail kèm trạng thái mở/đóng cửa (ShopHours).
- US-07: Lưu POI offline, tự động sync lên server khi có mạng.
- US-08: Quản lý Saved tab và tùy chỉnh ngôn ngữ/giao diện ở Settings tab.

---

## 4. Luồng Trải Nghiệm Cốt Lõi (4 giai đoạn)

### Giai đoạn 1: Khởi tạo thông minh

- Tạo SessionId ẩn danh.
- Nạp AppSettings (ngôn ngữ, theme) từ SQLite.
- Kiểm tra local data, trigger background sync (Routes, Zones, ShopHours) nếu có mạng.
- Pre-warm location để giảm latency.

### Giai đoạn 2: Home Tab (Khám phá Tour)

- Hiển thị danh sách Routes/Tours.
- Người dùng chọn tour và bấm Start Tour để vào map chi tiết.

### Giai đoạn 3: Tour Map Execution

- Render tất cả Zones của tour lên map.
- Theo dõi vị trí bằng Stream + Polling fallback.
- Tính khoảng cách Haversine, áp dụng Hysteresis + Debounce để trigger POI/audio.

### Giai đoạn 4: Detail, Saved, Settings

- Mở POI detail (mô tả, ảnh, ShopHours).
- Local-first favorite: ghi ngay vào SQLite (GuestFavorites), worker sync lên server khi online.
- Saved tab để quản lý điểm đã lưu, Settings tab để đổi ngôn ngữ/theme và quản lý audio offline.

---

## 5. Thông Số Kỹ Thuật Lõi

### 5.1 Advanced Location Engine

Yêu cầu bắt buộc để tránh nhảy sai điểm ở đô thị hẹp:

- Stream + Polling fallback:
  - Ưu tiên OS location event stream.
  - Nếu >10s không có event, kích hoạt polling để tránh đóng băng UI.
- Noise filtering:
  - Bộ lọc nghiêm ngặt: loại bỏ sai số > 12m (bình thường) và > 35m (môi trường cao tầng).
- Confirmation + Debounce:
  - 0.35 giây để bắt điểm dừng đầu tiên và 0.7 giây khi chuyển đổi nhanh giữa các điểm dừng.
- Hysteresis anti ping-pong:
  - Enter radius = 8m.
  - Exit radius = 10m (hệ số 1.25x).

### 5.2 Offline-first & Sync

Nguồn chân lý trên mobile:
- SQLite (LocalZone, LocalRoute, GuestFavorites).
- UI đọc trực tiếp từ SQLite, không render trực tiếp từ API.

Sync worker:
- Favorite add/remove:
  - Ghi Local ngay lập tức.
  - Đẩy vào queue.
  - Có mạng thì gọi API.
  - 200 OK thì đánh dấu synced.
- Data refresh:
  - Fetch API.
  - Replace SQLite.
  - Update UI qua event/binding.

---

## 6. Danh Sách Tính Năng

### 6.1 Mobile Client (.NET MAUI)

- Navigation UI: AppShell Bottom TabBar (Home, Saved, Settings).
- Tour execution: map integration, pin theo tour, user marker thời gian thực.
- Smart geofencing: logic tính khoảng cách trên mobile.
- Audio player: MP3 hoặc Text-to-Speech khi vào POI.
- Anonymous identity: generate GUID lần đầu mở app, lưu Preferences làm GuestId.
- State management: Open/Closed UI dựa trên giờ hệ thống và ShopHours.

### 6.2 Web Backend (ASP.NET Core 10)

- APIs cho mobile sync: Routes, Zones, ShopHours, Narrations, GuestFavorites.
- Admin Dashboard: CRUD Routes, CRUD Zones.
- Vendor Portal: cập nhật ShopHours và trạng thái hoạt động.

---

## 7. Yêu Cầu Phi Chức Năng

- Zero latency UI: thao tác favorite < 100ms nhờ local-first.
- Battery drain: location tracking không vượt 5-7% pin/giờ sử dụng liên tục.
- Data resiliency: không mất favorite/analytics log khi app bị force-kill.

---

## 8. Lộ Trình Triển Khai

### Phase 1 (Tuần 1-2): Core architecture & offline engine

- Thiết lập SQLite local models.
- Hoàn thiện startup flow: anonymous session, pre-warm location, background sync.
- Xây dựng local-first cho GuestFavorites.

### Phase 2 (Tuần 3-4): GPS engine

- Xây LocationService (Stream + Polling fallback).
- Xây GeofenceService (Noise filtering, Haversine, Hysteresis, Debounce).

### Phase 3 (Tuần 5-6): UI & journey

- Dựng AppShell tabs: Home -> Map -> POI Detail.
- Hiển thị Open/Closed theo ShopHours.
- Hoàn thiện Saved và Settings.

### Phase 4 (Tuần 7-8): Audio integration & field test

- Tích hợp TTS/MP3.
- Test thực địa tại Bùi Viện để tinh chỉnh Hysteresis + Debounce.

---

## Cấu Trúc Dự Án Hiện Tại

```text
TravelSystem.sln
├── src/TravelSystem.Mobile/
├── src/TravelSystem.Web/
└── src/TravelSystem.Shared/
```

Chi tiết tiến độ implementation xem tại CHECKLIST.md.
