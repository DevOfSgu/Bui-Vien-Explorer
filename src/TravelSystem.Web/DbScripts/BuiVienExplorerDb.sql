-- 1. Tạo bảng Shops (Cần tạo trước vì Users và Zones tham chiếu đến nó)
CREATE TABLE Shops (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Address NVARCHAR(255),
    PhoneNumber NVARCHAR(20),
    ImageUrl NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- 2. Tạo bảng Routes (Tuyến đường)
CREATE TABLE Routes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL, -- Ví dụ: "Bùi Viện Walking Tour"
    Description NVARCHAR(500),
    StartLatitude DECIMAL(10, 8),
    StartLongitude DECIMAL(11, 8),
    QRCode NVARCHAR(255),        -- UUID hoặc mã định danh cho QR
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE() -- [RECOMMEND] Thêm để phục vụ Sync
);
GO

-- 3. Tạo bảng Users (Quản trị viên & Vendor)
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE, -- Tên đăng nhập không được trùng
    PasswordHash NVARCHAR(255) NOT NULL,
    Role INT DEFAULT 0,          -- 0: Admin, 1: Vendor
    ShopId INT NULL,             -- Khóa ngoại liên kết với bảng Shops (Nullable)
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    
    -- Định nghĩa Khóa ngoại
    CONSTRAINT FK_Users_Shops FOREIGN KEY (ShopId) REFERENCES Shops(Id)
);
GO

-- 4. Tạo bảng Zones (Các địa điểm/điểm dừng)
CREATE TABLE Zones (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RouteId INT NOT NULL,        -- Thuộc tuyến đường nào
    ShopId INT NULL,             -- Nếu Zone này là một Shop cụ thể
    
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(1000),
    ImageUrl NVARCHAR(500),      -- Ảnh minh họa POI
    Latitude DECIMAL(10, 8) NOT NULL,
    Longitude DECIMAL(11, 8) NOT NULL,
    Radius INT DEFAULT 15,       -- Bán kính kích hoạt (mét)
    OrderIndex INT DEFAULT 0,    -- Thứ tự hiển thị trên lộ trình
    ZoneType INT DEFAULT 0,      -- 0:Bar, 1:Restaurant, 2:Landmark...
    
    -- Trạng thái & Logic
    IsActive BIT DEFAULT 1,      -- Admin tắt/bật
    ActiveTime INT DEFAULT 0,    -- [QUAN TRỌNG] 0:All, 1:Day, 2:Night (Như đã thảo luận)
    
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE(), -- [QUAN TRỌNG] Để Mobile App biết khi nào cần tải lại
    
    -- Định nghĩa Khóa ngoại
    CONSTRAINT FK_Zones_Routes FOREIGN KEY (RouteId) REFERENCES Routes(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Zones_Shops FOREIGN KEY (ShopId) REFERENCES Shops(Id)
);
GO

-- 5. Tạo bảng AudioFiles (File âm thanh & Script TTS)
CREATE TABLE AudioFiles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ZoneId INT NOT NULL,
    
    Language NVARCHAR(5) NOT NULL, -- "vi", "en", "ja"...
    TtsScript NVARCHAR(MAX),       -- Script thuyết minh dạng text (cho TTS engine)
    FileName NVARCHAR(255),
    FileUrl NVARCHAR(500),         -- Đường dẫn file MP3 trên Server/CDN
    Duration INT DEFAULT 0,        -- Thời lượng (giây)
    FileSize BIGINT,               -- Kích thước file (bytes)
    Version INT DEFAULT 1,         -- [RECOMMEND] Để App biết file có thay đổi không
    
    UploadedAt DATETIME DEFAULT GETDATE(),
    
    CONSTRAINT FK_AudioFiles_Zones FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE
);
GO


-- 6. Tạo bảng Analytics (Thống kê ẩn danh)
CREATE TABLE Analytics (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ZoneId INT NOT NULL,
    
    SessionId UNIQUEIDENTIFIER NOT NULL, -- Mã phiên ẩn danh (Guest ID)
    Language NVARCHAR(5),
    VisitDate DATE DEFAULT CAST(GETDATE() AS DATE), -- Chỉ lưu ngày, không cần giờ phút để query nhanh
    CreatedAt DATETIME DEFAULT GETDATE(), -- Thời gian chính xác log được tạo
    
    CONSTRAINT FK_Analytics_Zones FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE
);
GO

-- 7. Tạo Index (Để truy vấn nhanh cho báo cáo)
-- Giúp Admin xem báo cáo "Hôm nay bao nhiêu người vào Zone này" cực nhanh
CREATE INDEX IDX_Analytics_Zone_Date ON Analytics(ZoneId, VisitDate);
GO