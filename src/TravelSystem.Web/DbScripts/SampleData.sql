-- ============================================================
-- SAMPLE DATA - Bùi Viện Explorer (Đã mở rộng 20 record/bảng)
-- ============================================================
-- Xóa data cũ (nếu bảng tồn tại)
IF OBJECT_ID('GuestFavorites','U') IS NOT NULL DELETE FROM GuestFavorites;
IF OBJECT_ID('TourZones','U') IS NOT NULL DELETE FROM TourZones;
IF OBJECT_ID('Tours','U') IS NOT NULL DELETE FROM Tours;
IF OBJECT_ID('AppSettings','U') IS NOT NULL DELETE FROM AppSettings;

IF OBJECT_ID('ShopHours','U') IS NOT NULL DELETE FROM ShopHours;
IF OBJECT_ID('Narrations','U') IS NOT NULL DELETE FROM Narrations;
IF OBJECT_ID('Analytics','U') IS NOT NULL DELETE FROM Analytics;
IF OBJECT_ID('Zones','U') IS NOT NULL DELETE FROM Zones;
IF OBJECT_ID('Users','U') IS NOT NULL DELETE FROM Users;
IF OBJECT_ID('Shops','U') IS NOT NULL DELETE FROM Shops;


-- Reset Identity counters
-- AppSettings dùng Key (string) làm PK, không có identity -- không cần RESEED

IF OBJECT_ID('ShopHours','U') IS NOT NULL DBCC CHECKIDENT ('ShopHours', RESEED, 0);
IF OBJECT_ID('Shops','U') IS NOT NULL DBCC CHECKIDENT ('Shops', RESEED, 0);
IF OBJECT_ID('Zones','U') IS NOT NULL DBCC CHECKIDENT ('Zones', RESEED, 0);
IF OBJECT_ID('Tours','U') IS NOT NULL DBCC CHECKIDENT ('Tours', RESEED, 0);
IF OBJECT_ID('Narrations','U') IS NOT NULL DBCC CHECKIDENT ('Narrations', RESEED, 0);
IF OBJECT_ID('Users','U') IS NOT NULL DBCC CHECKIDENT ('Users', RESEED, 0);



-- ============================================================
-- 1. Insert 20 Shops
-- ============================================================
INSERT INTO Shops (Name, Address, PhoneNumber, ImageUrl)
VALUES (
        N'Bún chả 145',
        N'11 Bùi Viện, Q1, TP.HCM',
        N'028-3838-1111',
        NULL
    ),
    (
        N'Crazy Buffalo Bar',
        N'9 Bùi Viện, Q1, TP.HCM',
        N'028-3838-2222',
        NULL
    ),
    (
        N'Go2 Bar',
        N'187 Bùi Viện, Q1, TP.HCM',
        N'028-3838-3333',
        NULL
    ),
    (
        N'Boheme Pub',
        N'28 Bùi Viện, Q1, TP.HCM',
        N'028-3838-4444',
        NULL
    ),
    (
        N'Sahara Beer Club',
        N'111 Bùi Viện, Q1, TP.HCM',
        N'028-3838-5555',
        NULL
    ),
    (
        N'Miss Saigon',
        N'50 Bùi Viện, Q1, TP.HCM',
        N'028-3838-6666',
        NULL
    ),
    (
        N'Five Boys Number One',
        N'41 Bùi Viện, Q1, TP.HCM',
        N'028-3838-7777',
        NULL
    ),
    (
        N'Big Night Out - Bar & Lounge',
        N'95 Bùi Viện, Q1, TP.HCM',
        N'028-3838-8888',
        NULL
    ),
    (
        N'Đậu hũ nóng cô Thủy',
        N'45 Bùi Viện, Q1, TP.HCM',
        N'028-3838-9999',
        NULL
    ),
    (
        N'Nông Thôn Đại Việt - The rice restaurant',
        N'45 Bùi Viện, Q1, TP.HCM',
        N'028-3838-0000',
        NULL
    ),
    (
        N'Hair of the Dog',
        N'194 Bùi Viện, Q1, TP.HCM',
        N'028-3838-1122',
        NULL
    ),
    (
        N'Baba''s Kitchen Restaurant',
        N'139 Bùi Viện, Q1, TP.HCM',
        N'028-3838-2233',
        NULL
    ),
    (
        N'86 Phố Tây',
        N'86 Bùi Viện, Q1, TP.HCM',
        N'028-3838-3344',
        NULL
    ),
    (
        N'BBQ Saigon Night',
        N'175 Bùi Viện, Q1, TP.HCM',
        N'028-3838-4455',
        NULL
    ),
    (
        N'Bánh Căn Bùi Viện',
        N'155 Bùi Viện, Q1, TP.HCM',
        N'028-3838-5566',
        NULL
    ),
    (
        N'Crazy Night',
        N'80 Bùi Viện, Q1, TP.HCM',
        N'028-3838-6677',
        NULL
    ),
    (
        N'Station sport bar',
        N'115 Bùi Viện, Q1, TP.HCM',
        N'028-3838-7788',
        NULL
    ),
    (
        N'The View Rooftop Bar',
        N'195 Bùi Viện, Q1, TP.HCM',
        N'028-3838-8899',
        NULL
    ),
    (
        N'Corner Coffee & Beer',
        N'1 Bùi Viện, Q1, TP.HCM',
        N'028-3838-9900',
        NULL
    ),
    (
        N'Local Bùi Viện',
        N'33 Bùi Viện, Q1, TP.HCM',
        N'028-3838-0011',
        NULL
    );

-- ============================================================
-- 1a. Insert sample hours for shops (Mon-Sun, 8:00-23:00 open by default)
-- ============================================================
INSERT INTO ShopHours (ShopId, DayOfWeek, OpenTime, CloseTime)
SELECT Id, v.Number, '08:00', '23:00'
FROM Shops
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7)) v(Number);

-- ============================================================
-- 1b. System settings defaults
-- ============================================================
INSERT INTO AppSettings([Key],[Value],[UpdatedAt])
VALUES ('DefaultLanguage','vi', GETDATE()),
       ('EnableApiSync','1', GETDATE());

-- ============================================================
-- 2. Insert 20 Users (2 Admins, 18 Vendors)
-- ============================================================
INSERT INTO Users (Username, PasswordHash, FullName, Email, Role, ShopId, IsActive)
VALUES (N'admin', N'123456', N'Admin User', N'admin@buivienexplorer.com', 0, NULL, 1),
    (N'admin2', N'123456', N'Second Admin', N'admin2@buivienexplorer.com', 0, NULL, 1),
    (N'vendor1', N'123456', N'Vendor 1', N'vendor1@example.com', 1, 1, 1),
    (N'vendor2', N'123456', N'Vendor 2', N'vendor2@example.com', 1, 2, 1),
    (N'vendor3', N'123456', N'Vendor 3', N'vendor3@example.com', 1, 3, 1),
    (N'vendor4', N'123456', N'Vendor 4', N'vendor4@example.com', 1, 4, 1),
    (N'vendor5', N'123456', N'Vendor 5', N'vendor5@example.com', 1, 5, 1),
    (N'vendor6', N'123456', N'Vendor 6', N'vendor6@example.com', 1, 6, 1),
    (N'vendor7', N'123456', N'Vendor 7', N'vendor7@example.com', 1, 7, 1),
    (N'vendor8', N'123456', N'Vendor 8', N'vendor8@example.com', 1, 8, 1),
    (N'vendor9', N'123456', N'Vendor 9', N'vendor9@example.com', 1, 9, 1),
    (N'vendor10', N'123456', N'Vendor 10', N'vendor10@example.com', 1, 10, 1),
    (N'vendor11', N'123456', N'Vendor 11', N'vendor11@example.com', 1, 11, 1),
    (N'vendor12', N'123456', N'Vendor 12', N'vendor12@example.com', 1, 12, 1),
    (N'vendor13', N'123456', N'Vendor 13', N'vendor13@example.com', 1, 13, 1),
    (N'vendor14', N'123456', N'Vendor 14', N'vendor14@example.com', 1, 14, 1),
    (N'vendor15', N'123456', N'Vendor 15', N'vendor15@example.com', 1, 15, 1),
    (N'vendor16', N'123456', N'Vendor 16', N'vendor16@example.com', 1, 16, 1),
    (N'vendor17', N'123456', N'Vendor 17', N'vendor17@example.com', 1, 17, 1),
    (N'vendor18', N'123456', N'Vendor 18', N'vendor18@example.com', 1, 18, 1);

-- Re-map vendor1..vendor18 to the first 18 shops by actual Id order.
-- This avoids identity-offset issues (e.g., when Shops starts at 0).
;WITH shops_ranked AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn
    FROM Shops
),
vendors_ranked AS (
    SELECT
        u.Id,
        TRY_CONVERT(INT, SUBSTRING(u.Username, 7, 20)) AS vendor_no
    FROM Users u
    WHERE u.Role = 1
      AND u.Username LIKE N'vendor%'
)
UPDATE u
SET u.ShopId = s.Id
FROM Users u
JOIN vendors_ranked v ON v.Id = u.Id
JOIN shops_ranked s ON s.rn = v.vendor_no
WHERE v.vendor_no BETWEEN 1 AND 18;

-- ============================================================
-- 3. (Routes removed)
-- ============================================================


-- ============================================================
-- 4. Insert 20 Zones
-- ============================================================
INSERT INTO Zones (
        ShopId,
        Name,
        Description,
        Latitude,
        Longitude,
        Radius,
        OrderIndex,
        ZoneType,
        IsActive,
        ActiveTime,
        IsLocked,
        IsHidden,
        LockReason
    )

VALUES (
        NULL,

        N'Cổng chào Bùi Viện',
        N'Nơi đón khách tham quan chính thức của toàn phố.',
        10.76772,
        106.69400,
        20,
        1,
        3,
        1,
        0,
        0,
        0,
        NULL
    ),
    (
        1,

        N'Bún chả 145',
        N'Quán bún chả nổi tiếng, đông khách và dễ nhận diện ngay mặt tiền Bùi Viện.',
        10.76765,
        106.69380,
        15,
        2,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        2,

        N'Crazy Buffalo Bar',
        N'Biểu tượng nổi tiếng với mô hình trâu rừng.',
        10.76758,
        106.69358,
        15,
        3,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        NULL,

        N'Quảng trường giữa',
        N'Nơi hay có múa lửa nghệ thuật.',
        10.76748,
        106.69300,
        25,
        4,
        3,
        1,
        0,
        0,
        0,
        NULL
    ),
    (
        3,

        N'Go2 Bar',
        N'Bar nổi tiếng với bia rẻ và nhạc sôi động trên Bùi Viện.',
        10.76742,
        106.69272,
        15,
        5,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        4,

        N'Boheme Pub',
        N'Điểm đến cho sinh viên quẩy banh nóc.',
        10.76738,
        106.69250,
        15,
        6,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        5,

        N'Sahara Beer Club',
        N'Trải nghiệm bia hơi và nhạc DJ.',
        10.76733,
        106.69228,
        15,
        7,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        6,

        N'Miss Saigon',
        N'Phong cách sang trọng với âm nhạc hiện đại.',
        10.76728,
        106.69208,
        15,
        8,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        7,

        N'Five Boys Number One',
        N'Quán burger ăn đêm quen thuộc, được nhiều khách quốc tế lựa chọn.',
        10.76724,
        106.69188,
        15,
        9,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        8,

        N'Big Night Out - Bar & Lounge',
        N'Bar & lounge sôi động, phù hợp đi nhóm và giải trí về đêm.',
        10.76720,
        106.69170,
        15,
        10,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        9,

        N'Đậu hũ nóng cô Thủy',
        N'Điểm ăn vặt bình dân nổi tiếng với đậu hũ nóng và món Việt dân dã.',
        10.76716,
        106.69148,
        15,
        11,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        10,

        N'Nông Thôn Đại Việt - The rice restaurant',
        N'Nhà hàng cơm Việt với món quê truyền thống, phù hợp khách gia đình.',
        10.76712,
        106.69128,
        15,
        12,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        11,

        N'Hair of the Dog',
        N'Sôi động thâu đêm.',
        10.76708,
        106.69108,
        15,
        13,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        12,

        N'Baba''s Kitchen Restaurant',
        N'Nhà hàng phục vụ món quốc tế và món Việt dễ ăn, không gian thân thiện.',
        10.76704,
        106.69088,
        15,
        14,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        13,

        N'86 Phố Tây',
        N'Góc phố nhìn ra công viên 23/9.',
        10.76700,
        106.69068,
        15,
        15,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        14,

        N'BBQ Saigon Night',
        N'Quán BBQ về đêm với các món nướng đậm vị, không khí nhộn nhịp.',
        10.76696,
        106.69048,
        15,
        16,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        15,

        N'Bánh Căn Bùi Viện',
        N'Điểm ăn vặt chuyên bánh căn nóng giòn, phù hợp trải nghiệm street food.',
        10.76692,
        106.69028,
        15,
        17,
        1,
        1,
        0,
        0,
        0,
        NULL
    ),
    (
        16,

        N'Crazy Night',
        N'Club EDM nổi tiếng, nhạc mạnh, đèn laser sôi động.',
        10.76688,
        106.69008,
        15,
        18,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        17,

        N'Station sport bar',
        N'Sports bar có màn hình lớn, phù hợp xem trận đấu và tụ tập bạn bè.',
        10.76684,
        106.68988,
        15,
        19,
        0,
        1,
        2,
        0,
        0,
        NULL
    ),
    (
        18,

        N'The View Rooftop Bar',
        N'Rooftop bar với tầm nhìn toàn cảnh phố đi bộ Bùi Viện.',
        10.76680,
        106.68965,
        15,
        20,
        0,
        1,
        2,
        0,
        0,
        NULL
    );

-- ============================================================
-- 5. Insert 20 Narrations
-- ============================================================
DECLARE @z1 INT,@z2 INT,@z3 INT,@z4 INT,@z5 INT,@z6 INT,@z7 INT,@z8 INT,@z9 INT,@z10 INT,
        @z11 INT,@z12 INT,@z13 INT,@z14 INT,@z15 INT,@z16 INT,@z17 INT,@z18 INT,@z19 INT,@z20 INT;

SET ANSI_WARNINGS OFF;

WITH numbered AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn
    FROM Zones
)
SELECT
    @z1 = MAX(CASE WHEN rn = 1 THEN Id END),
    @z2 = MAX(CASE WHEN rn = 2 THEN Id END),
    @z3 = MAX(CASE WHEN rn = 3 THEN Id END),
    @z4 = MAX(CASE WHEN rn = 4 THEN Id END),
    @z5 = MAX(CASE WHEN rn = 5 THEN Id END),
    @z6 = MAX(CASE WHEN rn = 6 THEN Id END),
    @z7 = MAX(CASE WHEN rn = 7 THEN Id END),
    @z8 = MAX(CASE WHEN rn = 8 THEN Id END),
    @z9 = MAX(CASE WHEN rn = 9 THEN Id END),
    @z10 = MAX(CASE WHEN rn = 10 THEN Id END),
    @z11 = MAX(CASE WHEN rn = 11 THEN Id END),
    @z12 = MAX(CASE WHEN rn = 12 THEN Id END),
    @z13 = MAX(CASE WHEN rn = 13 THEN Id END),
    @z14 = MAX(CASE WHEN rn = 14 THEN Id END),
    @z15 = MAX(CASE WHEN rn = 15 THEN Id END),
    @z16 = MAX(CASE WHEN rn = 16 THEN Id END),
    @z17 = MAX(CASE WHEN rn = 17 THEN Id END),
    @z18 = MAX(CASE WHEN rn = 18 THEN Id END),
    @z19 = MAX(CASE WHEN rn = 19 THEN Id END),
    @z20 = MAX(CASE WHEN rn = 20 THEN Id END)
FROM numbered;

SET ANSI_WARNINGS ON;

IF EXISTS (SELECT 1 FROM Zones)
BEGIN
INSERT INTO Narrations (ZoneId, Language, Text, VoiceId, FileUrl, AudioStatus)
VALUES (
        @z1,
        N'vi',
        N'Chào mừng bạn đến với Cổng chào Bùi Viện. Hãy sẵn sàng trải nghiệm buổi tối thú vị.',
        N'vi-VN-Standard-A',
        N'/uploads/audio/1_vi.mp3',
        N'ready'
    ),
    (
        @z2,
        N'vi',
        N'Đây là Bún chả 145, quán ăn Việt nổi tiếng với hương vị đậm đà và đông khách mỗi tối.',
        N'vi-VN-Standard-A',
        N'/uploads/audio/2_vi.mp3',
        N'ready'
    ),
    (
        @z3,
        N'vi',
        N'Crazy Buffalo Bar có không gian cực đại. Hãy chụp một bức ảnh check in ở đây nhé.',
        N'vi-VN-Standard-A',
        N'/uploads/audio/3_vi.mp3',
        N'ready'
    ),
    (
        @z4,
        N'vi',
        N'Tại quảng trường trung tâm này, cuối tuần hay có múa lửa và biểu diễn ảo thuật.',
        N'vi-VN-Standard-A',
        N'/uploads/audio/4_vi.mp3',
        N'ready'
    ),
    (
        @z5,
        N'vi',
        N'Go2 Bar là một trong những bar lâu đời nhất Bùi Viện, bia rẻ và nhạc hay.',
        N'vi-VN-Standard-A',
        N'/uploads/audio/5_vi.mp3',
        N'ready'
    ),
    (
        @z6,
        N'vi',
        N'Boheme Pub có DJ chơi nhạc cực cháy. Hãy ghé vào thử 1 chai bia!',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z7,
        N'en',
        N'Welcome to Sahara Beer Club. Let''s have a cold beer and enjoy the vibe.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z8,
        N'en',
        N'This is Miss Saigon, where modern aesthetics meet traditional hospitality.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z9,
        N'en',
        N'Five Boys Number One is a late-night burger spot popular with international visitors.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z10,
        N'en',
        N'Big Night Out - Bar & Lounge is a lively nightlife stop for groups and late-evening hangouts.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z11,
        N'vi',
        N'Đậu hũ nóng cô Thủy là điểm ăn vặt bình dân nổi tiếng với món nóng và hương vị quen thuộc.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z12,
        N'vi',
        N'Nông Thôn Đại Việt - The rice restaurant phục vụ cơm Việt và món quê truyền thống trong không gian ấm cúng.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z13,
        N'en',
        N'Hair of the Dog is perfect for late night parties.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z14,
        N'en',
        N'Baba''s Kitchen Restaurant serves approachable international and Vietnamese dishes in a friendly setting.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z15,
        N'vi',
        N'Từ 86 Phố Tây bạn có thể ngồi nhìn ra khu công viên cực kỳ thoáng.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z16,
        N'vi',
        N'BBQ Saigon Night là nơi giao thoa văn hóa đường phố rất tuyệt vời.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z17,
        N'en',
        N'Bánh Căn Bùi Viện is a street-food stop known for hot, crispy mini pancakes served with dipping sauce.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    ),
    (
        @z18,
        N'vi',
        N'Crazy Night là club EDM sôi động nhất đoạn cuối Bùi Viện.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z19,
        N'vi',
        N'Station sport bar là điểm tụ tập xem thể thao với không khí sôi động vào buổi tối.',
        N'vi-VN-Standard-A',
        NULL,
        N'pending'
    ),
    (
        @z20,
        N'en',
        N'End your night at The View Rooftop Bar with a panoramic view of Bui Vien.',
        N'en-US-Standard-C',
        NULL,
        N'pending'
    );
END

-- ============================================================
-- 6. Insert Sample Tours
-- ============================================================
INSERT INTO Tours (Name, Description, Duration, ImageUrl)
VALUES (
    N'Nightlife & Pub Crawl', 
    N'Khám phá những quán bar sôi động nhất Bùi Viện về đêm.', 
    120, 
    N'/images/tours/4e32d559-c3b9-4b98-821e-f39678e5a79e.jpg'
),
(
    N'Street Food & Culture', 
    N'Hành trình trải nghiệm ẩm thực đường phố và văn hóa địa phương.', 
    90, 
    N'/images/tours/fb38ed4c-c419-401b-b6be-5be1c195d3b5.jpg'
);

-- ============================================================
-- 7. Insert TourZones (Assign zones to tours)
-- ============================================================
DECLARE @t1 INT, @t2 INT;
SELECT @t1 = Id FROM Tours WHERE Name = N'Nightlife & Pub Crawl';
SELECT @t2 = Id FROM Tours WHERE Name = N'Street Food & Culture';

-- Tour 1: Nightlife (Z1, @z2, @z3, @z6, @z7, @z11)
INSERT INTO TourZones (TourId, ZoneId, OrderIndex)
SELECT @t1, Id, CASE Id WHEN @z1 THEN 1 WHEN @z2 THEN 2 WHEN @z3 THEN 3 WHEN @z6 THEN 4 WHEN @z7 THEN 5 WHEN @z11 THEN 6 END
FROM Zones WHERE Id IN (@z1, @z2, @z3, @z6, @z7, @z11);

-- Tour 2: Street Food (Z1, @z5, @z15, @z17, @z20)
INSERT INTO TourZones (TourId, ZoneId, OrderIndex)
SELECT @t2, Id, CASE Id WHEN @z1 THEN 1 WHEN @z5 THEN 2 WHEN @z15 THEN 3 WHEN @z17 THEN 4 WHEN @z20 THEN 5 END
FROM Zones WHERE Id IN (@z1, @z5, @z15, @z17, @z20);

-- Kiểm tra kết quả
SELECT N'Zones' AS [Table], COUNT(*) AS [Count] FROM Zones
UNION ALL SELECT N'Narrations', COUNT(*) FROM Narrations
UNION ALL SELECT N'Shops', COUNT(*) FROM Shops
UNION ALL SELECT N'Users', COUNT(*) FROM Users
UNION ALL SELECT N'Tours', COUNT(*) FROM Tours
UNION ALL SELECT N'TourZones', COUNT(*) FROM TourZones;



