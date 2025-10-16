-- ============================================
-- Bảng DeviceToken - Lưu FCM tokens của thiết bị
-- ============================================

CREATE TABLE DeviceToken (
    token_id INT PRIMARY KEY IDENTITY(1,1),
    user_id INT NOT NULL,
    device_token NVARCHAR(500) NOT NULL,
    platform NVARCHAR(20) NOT NULL, -- 'ios' hoặc 'android'
    is_active BIT DEFAULT 1,
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_DeviceToken_User FOREIGN KEY (user_id) REFERENCES [User](user_id) ON DELETE CASCADE
);

-- Index để tìm kiếm nhanh theo user_id
CREATE INDEX IDX_DeviceToken_UserId ON DeviceToken(user_id);

-- Index để lọc theo trạng thái active
CREATE INDEX IDX_DeviceToken_IsActive ON DeviceToken(is_active);

-- Index kết hợp để query hiệu quả
CREATE INDEX IDX_DeviceToken_UserId_IsActive ON DeviceToken(user_id, is_active);

GO

-- Thêm comment cho bảng
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Bảng lưu trữ FCM device tokens của người dùng để gửi push notifications', 
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'DeviceToken';
GO

