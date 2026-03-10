-- ============================================================
-- SAMPLE DATA - Bùi Viện Explorer (Đã mở rộng 20 record/bảng)
-- ============================================================
-- Xóa data cũ
DELETE FROM ShopHours;
DELETE FROM Narrations;
DELETE FROM Analytics;
DELETE FROM Zones;
DELETE FROM Users;
DELETE FROM Routes;
DELETE FROM Shops;
-- Reset Identity counters
DBCC CHECKIDENT ('ShopHours', RESEED, 0);
DBCC CHECKIDENT ('Shops', RESEED, 0);
DBCC CHECKIDENT ('Routes', RESEED, 0);
DBCC CHECKIDENT ('Zones', RESEED, 0);
DBCC CHECKIDENT ('Narrations', RESEED, 0);
DBCC CHECKIDENT ('Users', RESEED, 0);
-- ============================================================
-- 1. Insert 20 Shops
-- ============================================================
INSERT INTO Shops (Name, Address, PhoneNumber, ImageUrl)
VALUES (
        N'The Hideout Bar',
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
        N'Spotted By Locals',
        N'5 Bùi Viện, Q1, TP.HCM',
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
        N'Ocean Club',
        N'41 Bùi Viện, Q1, TP.HCM',
        N'028-3838-7777',
        NULL
    ),
    (
        N'Donkey Bar',
        N'120 Bùi Viện, Q1, TP.HCM',
        N'028-3838-8888',
        NULL
    ),
    (
        N'Universal Pub',
        N'90 Bùi Viện, Q1, TP.HCM',
        N'028-3838-9999',
        NULL
    ),
    (
        N'Champion Sports Bar',
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
        N'Republic Club',
        N'200 Bùi Viện, Q1, TP.HCM',
        N'028-3838-2233',
        NULL
    ),
    (
        N'86 Club',
        N'86 Bùi Viện, Q1, TP.HCM',
        N'028-3838-3344',
        NULL
    ),
    (
        N'Le Pub',
        N'175 Bùi Viện, Q1, TP.HCM',
        N'028-3838-4455',
        NULL
    ),
    (
        N'Asiana Food Town',
        N'Khu ẩm thực ngầm 23/9, Q1, TP.HCM',
        N'028-3838-5566',
        NULL
    ),
    (
        N'Krystal Lounge',
        N'80 Bùi Viện, Q1, TP.HCM',
        N'028-3838-6677',
        NULL
    ),
    (
        N'Nubes Rooftop',
        N'115 Bùi Viện, Q1, TP.HCM',
        N'028-3838-7788',
        NULL
    ),
    (
        N'Sky Bar 360',
        N'99 Bùi Viện, Q1, TP.HCM',
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
INSERT INTO ShopHours (ShopId, DayOfWeek, OpenTime, CloseTime, IsOpen)
SELECT Id, v.Number, '08:00', '23:00', 1
FROM Shops
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7)) v(Number);

-- ============================================================
-- 2. Insert 20 Users (2 Admins, 18 Vendors)
-- ============================================================
INSERT INTO Users (Username, PasswordHash, Role, ShopId, IsActive)
VALUES (N'admin', N'123456', 0, NULL, 1),
    (N'admin2', N'123456', 0, NULL, 1),
    (N'vendor1', N'123456', 1, 1, 1),
    (N'vendor2', N'123456', 1, 2, 1),
    (N'vendor3', N'123456', 1, 3, 1),
    (N'vendor4', N'123456', 1, 4, 1),
    (N'vendor5', N'123456', 1, 5, 1),
    (N'vendor6', N'123456', 1, 6, 1),
    (N'vendor7', N'123456', 1, 7, 1),
    (N'vendor8', N'123456', 1, 8, 1),
    (N'vendor9', N'123456', 1, 9, 1),
    (N'vendor10', N'123456', 1, 10, 1),
    (N'vendor11', N'123456', 1, 11, 1),
    (N'vendor12', N'123456', 1, 12, 1),
    (N'vendor13', N'123456', 1, 13, 1),
    (N'vendor14', N'123456', 1, 14, 1),
    (N'vendor15', N'123456', 1, 15, 1),
    (N'vendor16', N'123456', 1, 16, 1),
    (N'vendor17', N'123456', 1, 17, 1),
    (N'vendor18', N'123456', 1, 18, 1);
-- ============================================================
-- 3. Insert 1 Route
-- ============================================================
INSERT INTO Routes (
        Name,
        Description,
        StartLatitude,
        StartLongitude,
        QRCode,
        IsActive
    )
VALUES (
        N'Bùi Viện Walking Tour',
        N'Khám phá phố đi bộ Bùi Viện từ đầu đến cuối.',
        10.76968,
        106.69156,
        N'route-1',
        1
    );
-- ============================================================
-- 4. Insert 20 Zones (Mỗi Route gán 1 Zone để đảm bảo có data)
-- ============================================================
INSERT INTO Zones (
        RouteId,
        ShopId,
        Name,
        Description,
        Latitude,
        Longitude,
        Radius,
        OrderIndex,
        ZoneType,
        IsActive,
        ActiveTime
    )
VALUES (
        1,
        NULL,
        N'Cổng chào Bùi Viện',
        N'Nơi đón khách tham quan chính thức của toàn phố.',
        10.76968,
        106.69156,
        20,
        1,
        3,
        1,
        0
    ),
    (
        1,
        1,
        N'The Hideout Bar',
        N'Quán bar lâu đời.',
        10.76945,
        106.69170,
        15,
        2,
        0,
        1,
        2
    ),
    (
        1,
        2,
        N'Crazy Buffalo Bar',
        N'Biểu tượng nổi tiếng với mô hình trâu rừng.',
        10.76930,
        106.69185,
        15,
        3,
        0,
        1,
        2
    ),
    (
        1,
        NULL,
        N'Quảng trường giữa',
        N'Nơi hay có múa lửa nghệ thuật.',
        10.76910,
        106.69200,
        25,
        4,
        3,
        1,
        0
    ),
    (
        1,
        3,
        N'Spotted By Locals',
        N'Nhà hàng có không khí lãng mạn.',
        10.76890,
        106.69215,
        15,
        5,
        1,
        1,
        0
    ),
    (
        1,
        4,
        N'Boheme Pub',
        N'Điểm đến cho sinh viên quẩy banh nóc.',
        10.76895,
        106.69225,
        15,
        6,
        0,
        1,
        2
    ),
    (
        1,
        5,
        N'Sahara Beer Club',
        N'Trải nghiệm bia hơi và nhạc DJ.',
        10.76890,
        106.69230,
        15,
        7,
        0,
        1,
        2
    ),
    (
        1,
        6,
        N'Miss Saigon',
        N'Phong cách sang trọng với âm nhạc hiện đại.',
        10.76885,
        106.69235,
        15,
        8,
        0,
        1,
        2
    ),
    (
        1,
        7,
        N'Ocean Club',
        N'Nổi bật với thiết kế xanh mát nhiệt đới.',
        10.76880,
        106.69240,
        15,
        9,
        0,
        1,
        2
    ),
    (
        1,
        8,
        N'Donkey Bar',
        N'Không gian nhỏ, ấm cúng.',
        10.76875,
        106.69245,
        15,
        10,
        0,
        1,
        2
    ),
    (
        1,
        9,
        N'Universal Pub',
        N'Nhạc sống Tây ba lô yêu thích.',
        10.76870,
        106.69250,
        15,
        11,
        0,
        1,
        2
    ),
    (
        1,
        10,
        N'Champion Sports Bar',
        N'Nơi xem bóng đá ngoại hạng Anh tốt nhất.',
        10.76865,
        106.69255,
        15,
        12,
        0,
        1,
        2
    ),
    (
        1,
        11,
        N'Hair of the Dog',
        N'Sôi động thâu đêm.',
        10.76860,
        106.69260,
        15,
        13,
        0,
        1,
        2
    ),
    (
        1,
        12,
        N'Republic Club',
        N'Sang trọng đẳng cấp.',
        10.76855,
        106.69265,
        15,
        14,
        0,
        1,
        2
    ),
    (
        1,
        13,
        N'86 Club',
        N'Góc phố nhìn ra công viên 23/9.',
        10.76850,
        106.69270,
        15,
        15,
        0,
        1,
        2
    ),
    (
        1,
        14,
        N'Le Pub',
        N'Khách Tây hay ngồi vỉa hè.',
        10.76845,
        106.69275,
        15,
        16,
        0,
        1,
        2
    ),
    (
        1,
        15,
        N'Asiana Food Town',
        N'Khu ẩm thực đa quốc gia.',
        10.76840,
        106.69280,
        15,
        17,
        1,
        1,
        0
    ),
    (
        1,
        16,
        N'Krystal Lounge',
        N'Thư giãn thưởng thức Shisha.',
        10.76835,
        106.69285,
        15,
        18,
        0,
        1,
        2
    ),
    (
        1,
        17,
        N'Nubes Rooftop',
        N'Từ tầng thượng bạn có thể ngắm Landmark 81.',
        10.76830,
        106.69290,
        15,
        19,
        0,
        1,
        2
    ),
    (
        1,
        18,
        N'Sky Bar 360',
        N'Nhạc House, chill.',
        10.76825,
        106.69295,
        15,
        20,
        0,
        1,
        2
    );
-- ============================================================
-- 5. Insert 20 Narrations (Gán cho 20 Zones, random ngôn ngữ)
-- ============================================================
INSERT INTO Narrations (ZoneId, Language, Text, VoiceId)
VALUES (
        1,
        N'vi',
        N'Chào mừng bạn đến với Cổng chào Bùi Viện. Hãy sẵn sàng trải nghiệm buổi tối thú vị.',
        N'vi-VN-Standard-A'
    ),
    (
        2,
        N'vi',
        N'Đây là The Hideout Bar, luôn là điểm nhậu lý tưởng của khách phương xa.',
        N'vi-VN-Standard-A'
    ),
    (
        3,
        N'vi',
        N'Crazy Buffalo Bar có không gian cực đại. Hãy chụp một bức ảnh check in ở đây nhé.',
        N'vi-VN-Standard-A'
    ),
    (
        4,
        N'vi',
        N'Tại quảng trường trung tâm này, cuối tuần hay có múa lửa và biểu diễn ảo thuật.',
        N'vi-VN-Standard-A'
    ),
    (
        5,
        N'vi',
        N'Spotted By Locals cung cấp các món ăn ngon sau khi dạo phố.',
        N'vi-VN-Standard-A'
    ),
    (
        6,
        N'vi',
        N'Boheme Pub có DJ chơi nhạc cực cháy. Hãy ghé vào thử 1 chai bia!',
        N'vi-VN-Standard-A'
    ),
    (
        7,
        N'en',
        N'Welcome to Sahara Beer Club. Let''s have a cold beer and enjoy the vibe.',
        N'en-US-Standard-C'
    ),
    (
        8,
        N'en',
        N'This is Miss Saigon, where modern aesthetics meet traditional hospitality.',
        N'en-US-Standard-C'
    ),
    (
        9,
        N'en',
        N'Ocean Club offers a tropical vibe right inside the crowded city.',
        N'en-US-Standard-C'
    ),
    (
        10,
        N'en',
        N'Donkey Bar is the best place to chill and talk with your friends.',
        N'en-US-Standard-C'
    ),
    (
        11,
        N'vi',
        N'Universal Pub là nơi hay có nhạc Acoustic.',
        N'vi-VN-Standard-A'
    ),
    (
        12,
        N'vi',
        N'Nếu bạn mê bóng đá, hãy dừng chân tại Champion Sports Bar vào cuối tuần.',
        N'vi-VN-Standard-A'
    ),
    (
        13,
        N'en',
        N'Hair of the Dog is perfect for late night parties.',
        N'en-US-Standard-C'
    ),
    (
        14,
        N'en',
        N'Republic Club is the premium option for EDM lovers.',
        N'en-US-Standard-C'
    ),
    (
        15,
        N'vi',
        N'Từ 86 Club bạn có thể ngồi nhìn ra khu công viên cực kỳ thoáng.',
        N'vi-VN-Standard-A'
    ),
    (
        16,
        N'vi',
        N'Le Pub là nơi giao thoa văn hóa đường phố rất tuyệt vời.',
        N'vi-VN-Standard-A'
    ),
    (
        17,
        N'en',
        N'Asiana Food Town is literally an underground food heaven.',
        N'en-US-Standard-C'
    ),
    (
        18,
        N'vi',
        N'Vào Krystal Lounge bạn sẽ thấy không khí rất tĩnh lăng nhẹ nhàng hơn.',
        N'vi-VN-Standard-A'
    ),
    (
        19,
        N'vi',
        N'Bạn muốn ngắm cảnh ư? Nhìn lên ngay, Nubes Rooftop ở trên lầu 6.',
        N'vi-VN-Standard-A'
    ),
    (
        20,
        N'en',
        N'End your night gracefully at Sky Bar 360 with a panoramic view.',
        N'en-US-Standard-C'
    );
-- Kiểm tra kết quả
SELECT N'Routes' AS [Table],
    COUNT(*) AS [Count]
FROM Routes
UNION ALL
SELECT N'Zones',
    COUNT(*)
FROM Zones
UNION ALL
SELECT N'Narrations',
    COUNT(*)
FROM Narrations
UNION ALL
SELECT N'Shops',
    COUNT(*)
FROM Shops
UNION ALL
SELECT N'Users',
    COUNT(*)
FROM Users;
