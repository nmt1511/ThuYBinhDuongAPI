# ThuYBinhDuong Veterinary Clinic API 🐾

## Giới thiệu
API quản lý phòng khám thú y ThuYBinhDuong được xây dựng với ASP.NET Core 9.0, cung cấp các chức năng quản lý cho khách hàng và quản trị viên phòng khám thú y.

## Tính năng chính

### 👥 Quản lý người dùng
- Đăng ký tài khoản khách hàng với thông tin đầy đủ
- Đăng nhập với JWT Authentication
- Phân quyền theo vai trò (Customer/Administrator)

### 🐕 Quản lý thú cưng
- Thêm, sửa, xóa thông tin thú cưng
- Xem danh sách thú cưng của khách hàng
- Tính toán tuổi tự động
- Validation business rules

### 📅 Quản lý lịch hẹn
- Đặt lịch hẹn khám cho thú cưng
- Theo dõi trạng thái lịch hẹn
- Hủy lịch hẹn (khi cho phép)
- Validation ngày giờ và business logic

### 👨‍⚕️ Quản lý bác sĩ và dịch vụ
- Xem danh sách bác sĩ
- Tìm kiếm dịch vụ với phân trang
- Lọc dịch vụ theo danh mục

### 📰 Tin tức và sự kiện
- Xem tin tức phòng khám
- Tìm kiếm theo nội dung và tags
- Phân trang và lọc

## Công nghệ sử dụng

- **Framework**: ASP.NET Core 9.0
- **Database**: SQL Server với Entity Framework Core
- **Authentication**: JWT Bearer Token
- **Documentation**: Swagger/OpenAPI
- **Architecture**: Clean Architecture, Repository Pattern

## Yêu cầu hệ thống

- .NET 9.0 SDK
- SQL Server 2019+ hoặc SQL Server Express
- Visual Studio 2022 hoặc VS Code
- Git

## Cài đặt và chạy project

### 1. Clone repository
```bash
git clone https://github.com/yourusername/ThuYBinhDuongAPI.git
cd ThuYBinhDuongAPI
```

### 2. Cấu hình Database
Cập nhật connection string trong `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ThuYBinhDuongDB;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### 3. Tạo database và migration
```bash
# Tạo migration (nếu chưa có)
dotnet ef migrations add InitialCreate

# Cập nhật database
dotnet ef database update
```

### 4. Cấu hình JWT
Cập nhật JWT settings trong `appsettings.json`:
```json
{
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "ThuYBinhDuongAPI",
    "Audience": "ThuYBinhDuongApp",
    "ExpiryHours": 24
  }
}
```

### 5. Chạy ứng dụng
```bash
dotnet run
```

Ứng dụng sẽ chạy tại:
- **HTTPS**: https://localhost:7001
- **HTTP**: http://localhost:5000
- **Swagger UI**: https://localhost:7001/swagger

## Cấu trúc Project

```
ThuYBinhDuongAPI/
├── Controllers/           # API Controllers
│   ├── UserController.cs         # Quản lý người dùng
│   ├── PetController.cs          # Quản lý thú cưng
│   ├── AppointmentController.cs  # Quản lý lịch hẹn
│   ├── DoctorController.cs       # API bác sĩ
│   ├── ServiceController.cs      # API dịch vụ
│   ├── NewsController.cs         # API tin tức
│   └── AuthorizeRoleAttribute.cs # Custom authorization
├── Models/               # Entity Models
│   ├── User.cs
│   ├── Customer.cs
│   ├── Pet.cs
│   ├── Appointment.cs
│   ├── Doctor.cs
│   ├── Service.cs
│   ├── News.cs
│   └── ThuybinhduongContext.cs
├── Data/Dtos/            # Data Transfer Objects
│   ├── UserResponseDto.cs
│   ├── PetResponseDto.cs
│   ├── AppointmentResponseDto.cs
│   └── ...
├── Services/             # Business Services
│   ├── IJwtService.cs
│   └── JwtService.cs
├── API_DOCUMENTATION.md  # Hướng dẫn tích hợp React Native
└── README.md            # File này
```

## Hệ thống phân quyền

### Roles
- **0**: Customer (Khách hàng)
- **1**: Administrator (Quản trị viên)

### Quyền truy cập
- **Customer**: Chỉ có thể quản lý thú cưng và lịch hẹn của chính mình
- **Administrator**: Có toàn quyền quản lý hệ thống

## API Endpoints

### Authentication
- `POST /api/user/register` - Đăng ký tài khoản
- `POST /api/user/login` - Đăng nhập
- `GET /api/user/profile` - Lấy thông tin profile

### Pet Management (Customer only)
- `GET /api/pet` - Lấy danh sách thú cưng
- `GET /api/pet/{id}` - Lấy chi tiết thú cưng
- `POST /api/pet` - Thêm thú cưng mới
- `PUT /api/pet/{id}` - Cập nhật thông tin thú cưng
- `DELETE /api/pet/{id}` - Xóa thú cưng

### Appointment Management (Customer only)
- `GET /api/appointment` - Lấy danh sách lịch hẹn
- `GET /api/appointment/{id}` - Lấy chi tiết lịch hẹn
- `POST /api/appointment` - Đặt lịch hẹn mới
- `DELETE /api/appointment/{id}` - Hủy lịch hẹn

### Support APIs
- `GET /api/doctor` - Danh sách bác sĩ
- `GET /api/service` - Danh sách dịch vụ với search
- `GET /api/service/dropdown` - Dịch vụ cho dropdown
- `GET /api/news` - Tin tức với search và phân trang

## Business Rules

### Appointment Status
- **0**: Chờ xác nhận - Customer có thể hủy
- **1**: Đã xác nhận - Chỉ admin có thể thay đổi
- **2**: Hoàn thành - Không thể thay đổi
- **3**: Đã hủy - Không thể thay đổi

### Validation Rules
- Không được đặt lịch hẹn trong quá khứ
- Không được trùng lịch hẹn cho cùng thú cưng
- Chỉ được hủy lịch hẹn khi status = 0
- Không được xóa thú cưng có lịch hẹn đang chờ/xác nhận

## Testing API

### 1. Sử dụng Swagger UI
Truy cập https://localhost:7001/swagger để test API trực tiếp

### 2. Sử dụng Postman/Thunder Client
Import file `ThuYBinhDuongAPI.http` để có sẵn các request mẫu

### 3. Test flow cơ bản
```bash
# 1. Đăng ký tài khoản
POST /api/user/register
{
  "username": "customer01",
  "password": "123456",
  "email": "test@example.com",
  "phoneNumber": "0123456789",
  "role": 0,
  "customerName": "Nguyễn Văn A",
  "address": "123 ABC Street",
  "gender": 0
}

# 2. Đăng nhập (lấy token)
POST /api/user/login
{
  "username": "customer01",
  "password": "123456"
}

# 3. Thêm thú cưng (với token)
POST /api/pet
Authorization: Bearer {token}
{
  "name": "Milu",
  "species": "Chó",
  "breed": "Golden Retriever",
  "birthDate": "2022-05-15"
}

# 4. Đặt lịch hẹn (với token)
POST /api/appointment
Authorization: Bearer {token}
{
  "petId": 1,
  "serviceId": 1,
  "appointmentDate": "2024-02-01",
  "appointmentTime": "10:00 AM",
  "weight": 15.5,
  "age": 2
}
```

## Sample Data

### Admin User
- Username: `admin`
- Password: `admin123`
- Role: 1 (Administrator)

### Sample Customer
- Username: `customer01`
- Password: `123456`
- Role: 0 (Customer)

## Troubleshooting

### Lỗi thường gặp

1. **Connection String Error**
   ```
   Cập nhật connection string trong appsettings.json
   Đảm bảo SQL Server đang chạy
   ```

2. **JWT Token Invalid**
   ```
   Kiểm tra SecretKey trong appsettings.json (ít nhất 32 ký tự)
   Đảm bảo token được gửi đúng format: "Bearer {token}"
   ```

3. **Entity Framework Errors**
   ```bash
   dotnet ef database update
   dotnet clean
   dotnet build
   ```

4. **CORS Issues**
   ```
   Cấu hình CORS trong Program.cs đã được thiết lập
   Kiểm tra origin của client
   ```

## Deployment

### 1. Build for Production
```bash
dotnet publish -c Release -o ./publish
```

### 2. Cấu hình Production
Cập nhật `appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Production connection string"
  },
  "JwtSettings": {
    "SecretKey": "Production secret key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### 3. IIS Deployment
- Copy folder `publish` to IIS wwwroot
- Cấu hình Application Pool (.NET 9.0)
- Thiết lập connection string production

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Documentation

- **API Documentation**: Xem file `API_DOCUMENTATION.md` cho hướng dẫn tích hợp React Native
- **Swagger UI**: Available at `/swagger` endpoint
- **Development Rules**: Xem `.cursor/rules/thuybinhduong-api-rules.mdc`

## Support

Nếu gặp vấn đề, vui lòng:
1. Kiểm tra phần Troubleshooting
2. Xem API Documentation
3. Tạo issue trên GitHub

## License

This project is licensed under the MIT License - see the LICENSE file for details.

---

**ThuYBinhDuong Veterinary Clinic API** - Chăm sóc thú cưng với công nghệ hiện đại 🐾 