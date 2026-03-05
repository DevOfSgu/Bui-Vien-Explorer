-- ============================================================
-- SAMPLE DATA - Bùi Viện Explorer
-- Chạy file này SAU KHI đã chạy BuiVienExplorerDb.sql
-- Lưu ý: Dùng N'...' cho chuỗi Unicode tiếng Việt
-- ============================================================
-- Xóa data cũ (nếu có) trước khi insert lại
DELETE FROM Narrations;
DELETE FROM Analytics;
DELETE FROM Zones;
DELETE FROM Users;
DELETE FROM Routes;
DELETE FROM Shops;
-- Reset Identity counters
DBCC CHECKIDENT ('Shops', RESEED, 0);
DBCC CHECKIDENT ('Routes', RESEED, 0);
DBCC CHECKIDENT ('Zones', RESEED, 0);
DBCC CHECKIDENT ('Narrations', RESEED, 0);
-- 1. Insert Shops
INSERT INTO Shops (Name, Address, PhoneNumber, ImageUrl)
VALUES (
        N'The Hideout Bar',
        N'N11 Bùi Viện, Quận 1, TP.HCM',
        N'028-3838-3838',
        NULL
    ),
    (
        N'Crazy Buffalo Bar',
        N'N9 Bùi Viện, Quận 1, TP.HCM',
        NULL,
        NULL
    ),
    (
        N'Spotted By Locals',
        N'N5 Bùi Viện, Quận 1, TP.HCM',
        NULL,
        NULL
    );
-- 1.5. Insert Users (Admin & Vendor)
-- Mật khẩu mặc định đều là "123456" (Trong thực tế cần băm MD5/SHA256, ở đây dùng plain text để test tạm thời)
INSERT INTO Users (Username, PasswordHash, Role, ShopId, IsActive)
VALUES (N'admin', N'123456', 0, NULL, 1),
    -- Role 0 = Admin
    (N'vendor1', N'123456', 1, 1, 1),
    -- Role 1 = Vendor (Quản lý The Hideout Bar)
    (N'vendor2', N'123456', 1, 3, 1);
-- Role 1 = Vendor (Quản lý Spotted By Locals)
-- 2. Insert Route
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
        N'Khám phá con phố đi bộ sôi động nhất Sài Gòn với lịch sử, ẩm thực và văn hóa đêm đặc sắc.',
        10.76968,
        106.69156,
        N'bui-vien-main-2024',
        1
    );
-- 3. Insert Zones (RouteId = 1)
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
        N'Cổng vào Phố Bùi Viện',
        N'Điểm khởi đầu của tuyến phố đi bộ Bùi Viện, nơi hội tụ của du khách trong và ngoài nước.',
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
        N'Một trong những quán bar lâu đời và nổi tiếng nhất phố Bùi Viện. Nổi tiếng với cocktail đặc sắc và nhạc live.',
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
        N'Quán bar phong cách phương Tây với không gian mở, bia tươi và các buổi biểu diễn âm nhạc sống động mỗi tối.',
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
        N'Quảng trường trung tâm Bùi Viện',
        N'Khu vực trung tâm của phố đi bộ, nơi diễn ra các sự kiện âm nhạc đường phố và trình diễn nghệ thuật.',
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
        N'Nhà hàng được khách Tây yêu thích với menu phong phú, kết hợp ẩm thực Việt Nam và quốc tế.',
        10.76890,
        106.69215,
        15,
        5,
        1,
        1,
        0
    );
-- 4. Insert Narrations - Tiếng Việt
INSERT INTO Narrations (ZoneId, Language, Text, VoiceId)
VALUES (
        1,
        N'vi',
        N'Chào mừng bạn đến với Phố Bùi Viện — con phố đi bộ sôi động và nổi tiếng nhất Sài Gòn! Nằm tại Quận 1, phố Bùi Viện là điểm hẹn lý tưởng của du khách trong và ngoài nước, đặc biệt về đêm khi toàn bộ tuyến phố bừng sáng với ánh đèn và âm nhạc. Hãy cùng khám phá những điểm thú vị trên tuyến phố này nhé!',
        N'vi-VN-Standard-A'
    ),
    (
        2,
        N'vi',
        N'Bạn đang đứng trước The Hideout Bar — một trong những quán bar lâu đời và được yêu thích nhất trên phố Bùi Viện. Quán nổi tiếng với thực đơn cocktail phong phú và không khí ấm cúng. Mở cửa từ 5 giờ chiều đến 2 giờ sáng mỗi ngày, đây là điểm đến không thể bỏ qua cho những ai yêu thích văn hóa bar Sài Gòn.',
        N'vi-VN-Standard-A'
    ),
    (
        3,
        N'vi',
        N'Chào mừng đến với Crazy Buffalo Bar! Đây là điểm đến sôi động với phong cách miền Tây hoang dã, phục vụ bia tươi chất lượng và các loại cocktail đặc sắc. Mỗi tối quán đều có biểu diễn nhạc live với các ban nhạc địa phương và quốc tế, tạo nên một không khí cực kỳ năng động và vui vẻ.',
        N'vi-VN-Standard-A'
    ),
    (
        4,
        N'vi',
        N'Đây là trái tim của Phố Bùi Viện — khu vực quảng trường trung tâm nơi mọi cuộc vui hội tụ. Vào mỗi cuối tuần, không gian này trở thành sân khấu lớn với các buổi biểu diễn âm nhạc đường phố, nghệ sĩ xiếc và các màn trình diễn nghệ thuật đặc sắc.',
        N'vi-VN-Standard-A'
    ),
    (
        5,
        N'vi',
        N'Spotted By Locals là nhà hàng được lòng cả khách nội địa lẫn du khách nước ngoài. Với thực đơn đa dạng kết hợp hương vị Việt Nam và quốc tế, không gian thoáng mát và nhân viên phục vụ thân thiện. Đây là lựa chọn hoàn hảo nếu bạn muốn thưởng thức một bữa ăn ngon sau khi khám phá phố Bùi Viện.',
        N'vi-VN-Standard-A'
    );
-- 5. Insert Narrations - Tiếng Anh
INSERT INTO Narrations (ZoneId, Language, Text, VoiceId)
VALUES (
        1,
        N'en',
        N'Welcome to Bui Vien Walking Street — the most vibrant and famous pedestrian street in Saigon! Located in District 1, Bui Vien is the perfect meeting point for both domestic and international travelers, especially at night when the entire street lights up with colorful lights and music.',
        N'en-US-Standard-C'
    ),
    (
        2,
        N'en',
        N'You are now at The Hideout Bar — one of the oldest and most beloved bars on Bui Vien Street. Known for its extensive cocktail menu and warm atmosphere, this bar is open from 5 PM to 2 AM daily. A must-visit destination for anyone who loves Saigon bar culture.',
        N'en-US-Standard-C'
    ),
    (
        3,
        N'en',
        N'Welcome to Crazy Buffalo Bar! This lively spot has a wild western theme, serving quality draft beer and special cocktails. Every evening features live music performances by local and international bands, creating an incredibly energetic and fun atmosphere.',
        N'en-US-Standard-C'
    ),
    (
        4,
        N'en',
        N'This is the heart of Bui Vien Street — the central plaza where all the fun converges. Every weekend, this space transforms into a grand stage with street music performances, acrobats, and spectacular art shows.',
        N'en-US-Standard-C'
    ),
    (
        5,
        N'en',
        N'Spotted By Locals is a restaurant loved by both locals and foreign tourists alike. With a diverse menu blending Vietnamese and international flavors, an airy atmosphere, and friendly service, this is the perfect choice if you want to enjoy a good meal after exploring Bui Vien Street.',
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
FROM Shops;
