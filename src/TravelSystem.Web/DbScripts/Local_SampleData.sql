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

IF OBJECT_ID('ShopHours','U') IS NOT NULL DBCC CHECKIDENT ('ShopHours', RESEED, 0);
IF OBJECT_ID('Shops','U') IS NOT NULL DBCC CHECKIDENT ('Shops', RESEED, 0);
IF OBJECT_ID('Zones','U') IS NOT NULL DBCC CHECKIDENT ('Zones', RESEED, 0);
IF OBJECT_ID('Tours','U') IS NOT NULL DBCC CHECKIDENT ('Tours', RESEED, 0);
IF OBJECT_ID('Narrations','U') IS NOT NULL DBCC CHECKIDENT ('Narrations', RESEED, 0);
IF OBJECT_ID('Users','U') IS NOT NULL DBCC CHECKIDENT ('Users', RESEED, 0);

-- ============================================================
-- 1. Shops
-- ============================================================
INSERT INTO Shops (Name, Address, PhoneNumber, ImageUrl) VALUES
(N'The Hideout Bar',      N'11 Bùi Viện, Q1, TP.HCM',              N'028-3838-1111', NULL),
(N'Crazy Buffalo Bar',    N'9 Bùi Viện, Q1, TP.HCM',               N'028-3838-2222', NULL),
(N'Spotted By Locals',    N'5 Bùi Viện, Q1, TP.HCM',               N'028-3838-3333', NULL),
(N'Boheme Pub',           N'28/2A Bùi Viện, Q1, TP.HCM',           N'028-3838-4444', NULL),
(N'Sahara Beer Club',     N'111 Bùi Viện, Q1, TP.HCM',             N'028-3838-5555', NULL),
(N'Miss Saigon',          N'50 Bùi Viện, Q1, TP.HCM',              N'028-3838-6666', NULL),
(N'Ocean Club',           N'41 Bùi Viện, Q1, TP.HCM',              N'028-3838-7777', NULL),
(N'Donkey Bar',           N'120 Bùi Viện, Q1, TP.HCM',             N'028-3838-8888', NULL),
(N'Universal Pub',        N'90 Bùi Viện, Q1, TP.HCM',              N'028-3838-9999', NULL),
(N'Champion Sports Bar',  N'45 Bùi Viện, Q1, TP.HCM',              N'028-3838-0000', NULL),
(N'Hair of the Dog',      N'194 Bùi Viện, Q1, TP.HCM',             N'028-3838-1122', NULL),
(N'Republic Club',        N'200 Bùi Viện, Q1, TP.HCM',             N'028-3838-2233', NULL),
(N'86 Club',              N'86 Bùi Viện, Q1, TP.HCM',              N'028-3838-3344', NULL),
(N'Le Pub',               N'175 Bùi Viện, Q1, TP.HCM',             N'028-3838-4455', NULL),
(N'Asiana Food Town',     N'Khu ẩm thực ngầm 23/9, Q1, TP.HCM',    N'028-3838-5566', NULL),
(N'Krystal Lounge',       N'80 Bùi Viện, Q1, TP.HCM',              N'028-3838-6677', NULL),
(N'Nubes Rooftop',        N'115 Bùi Viện, Q1, TP.HCM',             N'028-3838-7788', NULL),
(N'Sky Bar 360',          N'99 Bùi Viện, Q1, TP.HCM',              N'028-3838-8899', NULL),
(N'Corner Coffee & Beer', N'1 Bùi Viện, Q1, TP.HCM',               N'028-3838-9900', NULL),
(N'Local Bùi Viện',       N'33 Bùi Viện, Q1, TP.HCM',              N'028-3838-0011', NULL);

INSERT INTO ShopHours (ShopId, DayOfWeek, OpenTime, CloseTime)
SELECT Id, v.Number, '08:00', '23:00'
FROM Shops CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7)) v(Number);

INSERT INTO AppSettings([Key],[Value],[UpdatedAt]) VALUES
('DefaultLanguage','vi', GETDATE()),
('EnableApiSync','1', GETDATE());

-- ============================================================
-- 2. Users
-- ============================================================
INSERT INTO Users (Username, PasswordHash, FullName, Email, Role, ShopId, IsActive) VALUES
(N'admin',   N'123456', N'Admin User',    N'admin@buivienexplorer.com',  0, NULL, 1),
(N'admin2',  N'123456', N'Second Admin',  N'admin2@buivienexplorer.com', 0, NULL, 1),
(N'vendor1', N'123456', N'Vendor 1',  N'vendor1@example.com',  1, 1,  1),
(N'vendor2', N'123456', N'Vendor 2',  N'vendor2@example.com',  1, 2,  1),
(N'vendor3', N'123456', N'Vendor 3',  N'vendor3@example.com',  1, 3,  1),
(N'vendor4', N'123456', N'Vendor 4',  N'vendor4@example.com',  1, 4,  1),
(N'vendor5', N'123456', N'Vendor 5',  N'vendor5@example.com',  1, 5,  1),
(N'vendor6', N'123456', N'Vendor 6',  N'vendor6@example.com',  1, 6,  1),
(N'vendor7', N'123456', N'Vendor 7',  N'vendor7@example.com',  1, 7,  1),
(N'vendor8', N'123456', N'Vendor 8',  N'vendor8@example.com',  1, 8,  1),
(N'vendor9', N'123456', N'Vendor 9',  N'vendor9@example.com',  1, 9,  1),
(N'vendor10',N'123456', N'Vendor 10', N'vendor10@example.com', 1, 10, 1),
(N'vendor11',N'123456', N'Vendor 11', N'vendor11@example.com', 1, 11, 1),
(N'vendor12',N'123456', N'Vendor 12', N'vendor12@example.com', 1, 12, 1),
(N'vendor13',N'123456', N'Vendor 13', N'vendor13@example.com', 1, 13, 1),
(N'vendor14',N'123456', N'Vendor 14', N'vendor14@example.com', 1, 14, 1),
(N'vendor15',N'123456', N'Vendor 15', N'vendor15@example.com', 1, 15, 1),
(N'vendor16',N'123456', N'Vendor 16', N'vendor16@example.com', 1, 16, 1),
(N'vendor17',N'123456', N'Vendor 17', N'vendor17@example.com', 1, 17, 1),
(N'vendor18',N'123456', N'Vendor 18', N'vendor18@example.com', 1, 18, 1);

-- ============================================================
-- 4. Zones — tọa độ thực tế (ước lượng từ điểm neo 17 Bùi Viện)
-- ⚠ Cần xác nhận lại từng tọa độ trên Google Maps
-- Ghi chú cách tính:
--   Điểm neo: "17 Bùi Viện" = 10.767443, 106.694126  (từ Google Maps của bạn)
--   Crazy Buffalo (9 BV, góc Bùi Viện - Đề Thám): gần 17 BV → ~10.7674, 106.6941
--   Cổng chào: đầu phố phía Trần Hưng Đạo → ~10.7691, 106.6963
--   Các quán số cao (111, 120...) → về phía Cống Quỳnh → latitude giảm, longitude giảm
-- ============================================================
INSERT INTO Zones (ShopId, Name, Description, Latitude, Longitude, Radius, OrderIndex, ZoneType, IsActive, ActiveTime, IsLocked, IsHidden, LockReason)
VALUES
-- 1. Cổng chào — đầu phố phía Trần Hưng Đạo
--    Search Google Maps: "Cổng chào phố đi bộ Bùi Viện"
(NULL, N'Cổng chào Bùi Viện',
 N'Nơi đón khách tham quan chính thức của toàn phố.',
 10.765389, 106.690278,   -- ⚠ cần xác nhận
 20, 1, 3, 1, 0, 0, 0, NULL),

-- 2. The Hideout Bar — 11 Bùi Viện (số lẻ, cùng dãy với 17 BV)
--    Search Google Maps: "The Hideout Bar 11 Bui Vien"
(1, N'The Hideout Bar',
 N'Quán bar lâu đời.',
 10.76748, 106.69415,   -- ⚠ cần xác nhận (ước lượng gần 17 BV)
 15, 2, 0, 1, 2, 0, 0, NULL),

-- 3. Crazy Buffalo — 9 Bùi Viện, góc Bùi Viện - Đề Thám
--    Search Google Maps: "Crazy Buffalo Bar Bui Vien"
(2, N'Crazy Buffalo Bar',
 N'Biểu tượng nổi tiếng với mô hình trâu rừng.',
 10.76744, 106.69413,   -- ⚠ cần xác nhận (gần 17 BV nhất)
 15, 3, 0, 1, 2, 0, 0, NULL),

-- 4. Quảng trường giữa — khu vực giữa phố
--    Search Google Maps: "Bui Vien Walking Street Ho Chi Minh"
(NULL, N'Quảng trường giữa',
 N'Nơi hay có múa lửa nghệ thuật.',
 10.76820, 106.69520,   -- ⚠ cần xác nhận
 25, 4, 3, 1, 0, 0, 0, NULL),

-- 5. Spotted By Locals — 5 Bùi Viện
--    Search Google Maps: "Spotted By Locals Bui Vien"
(3, N'Spotted By Locals',
 N'Nhà hàng có không khí lãng mạn.',
 10.76750, 106.69418,   -- ⚠ cần xác nhận
 15, 5, 1, 1, 0, 0, 0, NULL),

-- 6. Boheme Pub — 28/2A Bùi Viện
--    Search Google Maps: "Boheme Pub Bui Vien"
(4, N'Boheme Pub',
 N'Điểm đến cho sinh viên quẩy banh nóc.',
 10.76745, 106.69410,   -- ⚠ cần xác nhận (số chẵn, đối diện 17 BV)
 15, 6, 0, 1, 2, 0, 0, NULL),

-- 7. Sahara Beer Club — 111 Bùi Viện
--    Search Google Maps: "Sahara Beer Club Bui Vien"
(5, N'Sahara Beer Club',
 N'Trải nghiệm bia hơi và nhạc DJ.',
 10.76680, 106.69310,   -- ⚠ cần xác nhận (số 111, về phía Cống Quỳnh)
 15, 7, 0, 1, 2, 0, 0, NULL),

-- 8. Miss Saigon — 50 Bùi Viện
--    Search Google Maps: "Miss Saigon Bar 50 Bui Vien"
(6, N'Miss Saigon',
 N'Phong cách sang trọng với âm nhạc hiện đại.',
 10.76715, 106.69365,   -- ⚠ cần xác nhận
 15, 8, 0, 1, 2, 0, 0, NULL),

-- 9. Ocean Club — 41 Bùi Viện
--    Search Google Maps: "Ocean Club 41 Bui Vien"
(7, N'Ocean Club',
 N'Nổi bật với thiết kế xanh mát nhiệt đới.',
 10.76720, 106.69370,   -- ⚠ cần xác nhận
 15, 9, 0, 1, 2, 0, 0, NULL),

-- 10. Donkey Bar — 120 Bùi Viện
--     Search Google Maps: "Donkey Bar 120 Bui Vien"
(8, N'Donkey Bar',
 N'Không gian nhỏ, ấm cúng.',
 10.76672, 106.69300,   -- ⚠ cần xác nhận (số 120, gần Cống Quỳnh)
 15, 10, 0, 1, 2, 0, 0, NULL),

-- 11. Universal Pub — 90 Bùi Viện
--     Search Google Maps: "Universal Pub 90 Bui Vien"
(9, N'Universal Pub',
 N'Nhạc sống Tây ba lô yêu thích.',
 10.76690, 106.69320,   -- ⚠ cần xác nhận
 15, 11, 0, 1, 2, 0, 0, NULL),

-- 12. Champion Sports Bar — 45 Bùi Viện
(10, N'Champion Sports Bar',
 N'Nơi xem bóng đá ngoại hạng Anh tốt nhất.',
 10.76718, 106.69368,   -- ⚠ cần xác nhận
 15, 12, 0, 1, 2, 0, 0, NULL),

-- 13. Hair of the Dog — 194 Bùi Viện
(11, N'Hair of the Dog',
 N'Sôi động thâu đêm.',
 10.76660, 106.69280,   -- ⚠ cần xác nhận (số 194, gần cuối phố)
 15, 13, 0, 1, 2, 0, 0, NULL),

-- 14. Republic Club — 200 Bùi Viện
(12, N'Republic Club',
 N'Sang trọng đẳng cấp.',
 10.76655, 106.69275,   -- ⚠ cần xác nhận
 15, 14, 0, 1, 2, 0, 0, NULL),

-- 15. 86 Club — 86 Bùi Viện
(13, N'86 Club',
 N'Góc phố nhìn ra công viên 23/9.',
 10.76693, 106.69325,   -- ⚠ cần xác nhận
 15, 15, 0, 1, 2, 0, 0, NULL),

-- 16. Le Pub — 175 Bùi Viện
(14, N'Le Pub',
 N'Khách Tây hay ngồi vỉa hè.',
 10.76663, 106.69285,   -- ⚠ cần xác nhận
 15, 16, 0, 1, 2, 0, 0, NULL),

-- 17. Asiana Food Town — khu 23/9
--     Search Google Maps: "Asiana Food Town 23/9 Ho Chi Minh"
(15, N'Asiana Food Town',
 N'Khu ẩm thực đa quốc gia.',
 10.76730, 106.69200,   -- ⚠ cần xác nhận (gần công viên 23/9)
 15, 17, 1, 1, 0, 0, 0, NULL),

-- 18. Krystal Lounge — 80 Bùi Viện
(16, N'Krystal Lounge',
 N'Thư giãn thưởng thức Shisha.',
 10.76697, 106.69330,   -- ⚠ cần xác nhận
 15, 18, 0, 1, 2, 0, 0, NULL),

-- 19. Nubes Rooftop — 115 Bùi Viện
(17, N'Nubes Rooftop',
 N'Từ tầng thượng bạn có thể ngắm Landmark 81.',
 10.76677, 106.69308,   -- ⚠ cần xác nhận
 15, 19, 0, 1, 2, 0, 0, NULL),

-- 20. Sky Bar 360 — 99 Bùi Viện
(18, N'Sky Bar 360',
 N'Nhạc House, chill.',
 10.76686, 106.69318,   -- ⚠ cần xác nhận
 15, 20, 0, 1, 2, 0, 0, NULL);

-- ============================================================
-- 5. Narrations
-- ============================================================
DECLARE @z1 INT,@z2 INT,@z3 INT,@z4 INT,@z5 INT,@z6 INT,@z7 INT,@z8 INT,@z9 INT,@z10 INT,
        @z11 INT,@z12 INT,@z13 INT,@z14 INT,@z15 INT,@z16 INT,@z17 INT,@z18 INT,@z19 INT,@z20 INT;

SET ANSI_WARNINGS OFF;
WITH numbered AS (SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn FROM Zones)
SELECT
    @z1=MAX(CASE WHEN rn=1 THEN Id END), @z2=MAX(CASE WHEN rn=2 THEN Id END),
    @z3=MAX(CASE WHEN rn=3 THEN Id END), @z4=MAX(CASE WHEN rn=4 THEN Id END),
    @z5=MAX(CASE WHEN rn=5 THEN Id END), @z6=MAX(CASE WHEN rn=6 THEN Id END),
    @z7=MAX(CASE WHEN rn=7 THEN Id END), @z8=MAX(CASE WHEN rn=8 THEN Id END),
    @z9=MAX(CASE WHEN rn=9 THEN Id END), @z10=MAX(CASE WHEN rn=10 THEN Id END),
    @z11=MAX(CASE WHEN rn=11 THEN Id END),@z12=MAX(CASE WHEN rn=12 THEN Id END),
    @z13=MAX(CASE WHEN rn=13 THEN Id END),@z14=MAX(CASE WHEN rn=14 THEN Id END),
    @z15=MAX(CASE WHEN rn=15 THEN Id END),@z16=MAX(CASE WHEN rn=16 THEN Id END),
    @z17=MAX(CASE WHEN rn=17 THEN Id END),@z18=MAX(CASE WHEN rn=18 THEN Id END),
    @z19=MAX(CASE WHEN rn=19 THEN Id END),@z20=MAX(CASE WHEN rn=20 THEN Id END)
FROM numbered;
SET ANSI_WARNINGS ON;

IF EXISTS (SELECT 1 FROM Zones)
BEGIN
INSERT INTO Narrations (ZoneId, Language, Text, VoiceId) VALUES
(@z1,  N'vi', N'Chào mừng bạn đến với Cổng chào Bùi Viện. Hãy sẵn sàng trải nghiệm buổi tối thú vị.', N'vi-VN-Standard-A'),
(@z2,  N'vi', N'Đây là The Hideout Bar, luôn là điểm nhậu lý tưởng của khách phương xa.',              N'vi-VN-Standard-A'),
(@z3,  N'vi', N'Crazy Buffalo Bar có không gian cực đại. Hãy chụp một bức ảnh check in ở đây nhé.',    N'vi-VN-Standard-A'),
(@z4,  N'vi', N'Tại quảng trường trung tâm này, cuối tuần hay có múa lửa và biểu diễn ảo thuật.',       N'vi-VN-Standard-A'),
(@z5,  N'vi', N'Spotted By Locals cung cấp các món ăn ngon sau khi dạo phố.',                           N'vi-VN-Standard-A'),
(@z6,  N'vi', N'Boheme Pub có DJ chơi nhạc cực cháy. Hãy ghé vào thử 1 chai bia!',                     N'vi-VN-Standard-A'),
(@z7,  N'en', N'Welcome to Sahara Beer Club. Let''s have a cold beer and enjoy the vibe.',               N'en-US-Standard-C'),
(@z8,  N'en', N'This is Miss Saigon, where modern aesthetics meet traditional hospitality.',             N'en-US-Standard-C'),
(@z9,  N'en', N'Ocean Club offers a tropical vibe right inside the crowded city.',                       N'en-US-Standard-C'),
(@z10, N'en', N'Donkey Bar is the best place to chill and talk with your friends.',                      N'en-US-Standard-C'),
(@z11, N'vi', N'Universal Pub là nơi hay có nhạc Acoustic.',                                             N'vi-VN-Standard-A'),
(@z12, N'vi', N'Nếu bạn mê bóng đá, hãy dừng chân tại Champion Sports Bar vào cuối tuần.',              N'vi-VN-Standard-A'),
(@z13, N'en', N'Hair of the Dog is perfect for late night parties.',                                     N'en-US-Standard-C'),
(@z14, N'en', N'Republic Club is the premium option for EDM lovers.',                                    N'en-US-Standard-C'),
(@z15, N'vi', N'Từ 86 Club bạn có thể ngồi nhìn ra khu công viên cực kỳ thoáng.',                      N'vi-VN-Standard-A'),
(@z16, N'vi', N'Le Pub là nơi giao thoa văn hóa đường phố rất tuyệt vời.',                              N'vi-VN-Standard-A'),
(@z17, N'en', N'Asiana Food Town is literally an underground food heaven.',                               N'en-US-Standard-C'),
(@z18, N'vi', N'Vào Krystal Lounge bạn sẽ thấy không khí rất tĩnh lặng nhẹ nhàng hơn.',               N'vi-VN-Standard-A'),
(@z19, N'vi', N'Bạn muốn ngắm cảnh ư? Nhìn lên ngay, Nubes Rooftop ở trên lầu 6.',                     N'vi-VN-Standard-A'),
(@z20, N'en', N'End your night gracefully at Sky Bar 360 with a panoramic view.',                        N'en-US-Standard-C');
END

-- ============================================================
-- 6. Tours
-- ============================================================
INSERT INTO Tours (Name, Description, Duration, ImageUrl) VALUES
(N'Nightlife & Pub Crawl',   N'Khám phá những quán bar sôi động nhất Bùi Viện về đêm.',               120, N'https://images.unsplash.com/photo-1514525253361-bee8d41deeb4'),
(N'Street Food & Culture',   N'Hành trình trải nghiệm ẩm thực đường phố và văn hóa địa phương.',       90,  N'https://images.unsplash.com/photo-1504674900247-0877df9cc836');

-- ============================================================
-- 7. TourZones
-- ============================================================
-- Tour 1: Nightlife — Z1, Z3, Z6, Z7, Z11 (đã bỏ The Hideout Bar @z2)
INSERT INTO TourZones (TourId, ZoneId, OrderIndex)
SELECT 1, Id, CASE Id
    WHEN @z1  THEN 1  -- Cổng chào Bùi Viện
    WHEN @z3  THEN 2  -- Crazy Buffalo Bar
    WHEN @z6  THEN 3  -- Boheme Pub
    WHEN @z7  THEN 4  -- Sahara Beer Club
    WHEN @z11 THEN 5  -- Universal Pub
END
FROM Zones WHERE Id IN (@z1, @z3, @z6, @z7, @z11);

-- Tour 2: Street Food
INSERT INTO TourZones (TourId, ZoneId, OrderIndex)
SELECT 2, Id, CASE Id
    WHEN @z1  THEN 1
    WHEN @z5  THEN 2
    WHEN @z15 THEN 3
    WHEN @z17 THEN 4
    WHEN @z20 THEN 5
END
FROM Zones WHERE Id IN (@z1, @z5, @z15, @z17, @z20);

-- ============================================================
-- KIỂM TRA KẾT QUẢ
-- ============================================================
SELECT N'Zones' AS [Table], COUNT(*) AS [Count] FROM Zones
UNION ALL SELECT N'Narrations', COUNT(*) FROM Narrations
UNION ALL SELECT N'Shops',      COUNT(*) FROM Shops
UNION ALL SELECT N'Users',      COUNT(*) FROM Users
UNION ALL SELECT N'Tours',      COUNT(*) FROM Tours
UNION ALL SELECT N'TourZones',  COUNT(*) FROM TourZones;

-- Xem tọa độ — paste từng dòng vào Google Maps để xác nhận
SELECT Name, Latitude, Longitude,
    CASE
        WHEN Latitude BETWEEN 10.765 AND 10.771
         AND Longitude BETWEEN 106.690 AND 106.698
        THEN N'✅ Trong vùng Bùi Viện'
        ELSE N'❌ Ngoài vùng — kiểm tra lại'
    END AS KiemTra
FROM Zones ORDER BY OrderIndex;
