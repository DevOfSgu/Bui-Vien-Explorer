-- ============================================================
-- SCHEMA - Bùi Viện Explorer (SQL Server)
-- Chạy đúng DB: BuiVienExplorerDb
-- ============================================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'BuiVienExplorerDb')
BEGIN
    CREATE DATABASE BuiVienExplorerDb;
END
GO

USE BuiVienExplorerDb;
-- RESET: Xóa bảng cũ (nếu có) trước khi tạo lại
-- Thứ tự xóa: bảng con trước, bảng cha sau (tránh lỗi FK)
-- ============================================================
IF OBJECT_ID('GuestFavorites', 'U') IS NOT NULL DROP TABLE GuestFavorites;
IF OBJECT_ID('Analytics', 'U') IS NOT NULL DROP TABLE Analytics;
IF OBJECT_ID('Narrations', 'U') IS NOT NULL DROP TABLE Narrations;
IF OBJECT_ID('AudioFiles', 'U') IS NOT NULL DROP TABLE AudioFiles;
IF OBJECT_ID('Zones', 'U') IS NOT NULL DROP TABLE Zones;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('ShopHours', 'U') IS NOT NULL DROP TABLE ShopHours;
IF OBJECT_ID('Shops', 'U') IS NOT NULL DROP TABLE Shops;
IF OBJECT_ID('AppSettings', 'U') IS NOT NULL DROP TABLE AppSettings;

-- ============================================================
-- 1. Shops (Cần tạo trước vì Users và Zones tham chiếu đến nó)
-- ============================================================
CREATE TABLE Shops (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Address NVARCHAR(255),
    PhoneNumber NVARCHAR(20),
    ImageUrl NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE()
);
-- ============================================================
-- 1a. ShopHours (Giờ mở cửa của các shop)
-- ============================================================
CREATE TABLE ShopHours (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    ShopId INT NOT NULL,
    DayOfWeek INT NOT NULL, -- 1=Monday, 2=Tuesday, ..., 7=Sunday
    OpenTime TIME,
    CloseTime TIME,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_ShopHours_Shops FOREIGN KEY (ShopId) REFERENCES Shops(Id) ON DELETE CASCADE
);
-- ============================================================
-- 2. Shops (Cần tạo trước vì Users và Zones tham chiếu đến nó)
-- ============================================================

-- ============================================================
-- 3. Users (Quản trị viên & Vendor)
-- ============================================================
CREATE TABLE Users (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    Email NVARCHAR(100),
    Role INT DEFAULT 0,
    -- 0: Admin, 1: Vendor
    ShopId INT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Users_Shops FOREIGN KEY (ShopId) REFERENCES Shops(Id)
);
-- ============================================================
-- 4. Zones (Các điểm dừng / POI)
-- ============================================================
CREATE TABLE Zones (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    ShopId INT NULL,

    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(1000),
    ImageUrl NVARCHAR(500),
    Latitude DECIMAL(10, 8) NOT NULL,
    Longitude DECIMAL(11, 8) NOT NULL,
    Radius INT DEFAULT 15,
    -- Bán kính kích hoạt (mét)
    OrderIndex INT DEFAULT 0,
    -- Thứ tự hiển thị trên lộ trình
    ZoneType INT DEFAULT 0,
    -- 0:Bar, 1:Restaurant, 2:Landmark...
    IsActive BIT DEFAULT 1,
    IsLocked BIT DEFAULT 0,
    IsHidden BIT DEFAULT 0,
    LockReason NVARCHAR(500),
    ActiveTime INT DEFAULT 0,
    -- 0:All, 1:Day, 2:Night
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Zones_Shops FOREIGN KEY (ShopId) REFERENCES Shops(Id)
);
-- ============================================================
-- 5. Narrations (Kịch bản TTS đa ngôn ngữ)
-- ============================================================
CREATE TABLE Narrations (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    ZoneId INT NOT NULL,
    Language NVARCHAR(5) NOT NULL,
    -- "vi", "en", "ja", "ko"...
    Text NVARCHAR(MAX),
    -- Nội dung kịch bản thuyết minh
    VoiceId NVARCHAR(50),
    -- "vi-VN-Standard-A", "en-US-Wavenet-D"
    ApprovalStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    -- "Pending", "Approved", "Rejected"
    UpdatedAt DATETIME DEFAULT GETDATE(),
    UpdatedBy INT NULL,
    -- Admin/Vendor nào cập nhật
    CONSTRAINT FK_Narrations_Zones FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Narrations_Users FOREIGN KEY (UpdatedBy) REFERENCES Users(Id)
);
-- ============================================================
-- 6. Analytics (Thống kê ẩn danh từ Mobile App)
-- ============================================================
CREATE TABLE Analytics (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    ZoneId INT NULL,
    -- Null khi ActionType = 'LocationPing'
    SessionId UNIQUEIDENTIFIER NOT NULL,

    -- UUID ẩn danh tạo từ Mobile
    Latitude DECIMAL(10, 8),
    -- Cần cho Heatmap
    Longitude DECIMAL(11, 8),
    -- Cần cho Heatmap
    ActionType NVARCHAR(50) NOT NULL,
    -- "EnterZone", "PlayNarration", "LocationPing"
    DwellTimeSeconds INT DEFAULT 0,
    -- Thời gian ở lại POI (giây)
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Analytics_Zones FOREIGN KEY (ZoneId) REFERENCES Zones(Id)
);
-- ============================================================
-- 7. GuestFavorites (Zones yêu thích của guest không đăng nhập)
-- ============================================================
CREATE TABLE GuestFavorites (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    GuestId   NVARCHAR(36) NOT NULL,           -- UUID lưu trong localStorage / MAUI Preferences
    ZoneId    INT NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_GuestFav_Zone FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_GuestFav UNIQUE (GuestId, ZoneId)
);
-- ============================================================
-- 8. Indexes (Tối ưu truy vấn báo cáo)
-- ============================================================
CREATE INDEX IDX_Analytics_Zone ON Analytics(ZoneId, ActionType);
CREATE INDEX IDX_Analytics_Location ON Analytics(Latitude, Longitude);
CREATE INDEX IDX_Analytics_Session ON Analytics(SessionId, CreatedAt);
CREATE INDEX IX_GuestFav_GuestId ON GuestFavorites(GuestId);
CREATE INDEX IX_GuestFav_ZoneId  ON GuestFavorites(ZoneId);

-- ============================================================
-- 9. AppSettings (Cài đặt hệ thống)
-- ============================================================
CREATE TABLE AppSettings (
    Id INT IDENTITY(1, 1) PRIMARY KEY,
    [Key] NVARCHAR(100) NOT NULL UNIQUE,
    [Value] NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
);