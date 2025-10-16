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

- **Framework: ASP.NET Core 9.0**  
  Nền tảng phát triển ứng dụng web hiện đại của Microsoft, hỗ trợ xây dựng RESTful API mạnh mẽ, bảo mật, hiệu năng cao, dễ mở rộng và bảo trì.

- **Ngôn ngữ lập trình: C#**  
  Ngôn ngữ chính của .NET, cú pháp rõ ràng, hỗ trợ lập trình hướng đối tượng, phù hợp cho phát triển backend.

- **Database: SQL Server**  
  Hệ quản trị cơ sở dữ liệu quan hệ mạnh mẽ, dễ tích hợp với .NET, đảm bảo an toàn và hiệu suất lưu trữ dữ liệu.

- **Entity Framework Core**  
  ORM (Object-Relational Mapping) giúp thao tác dữ liệu dưới dạng đối tượng, tự động sinh migration, giảm lỗi truy vấn SQL thủ công.

- **JWT Authentication (Microsoft.AspNetCore.Authentication.JwtBearer)**  
  Cơ chế xác thực hiện đại, bảo mật, không lưu trạng thái, phù hợp cho API, giúp phân quyền truy cập linh hoạt giữa khách hàng và quản trị viên.

- **Swagger/OpenAPI (Swashbuckle.AspNetCore)**  
  Tự động sinh tài liệu API, hỗ trợ test trực tiếp trên giao diện web, giúp lập trình viên và tester dễ dàng kiểm thử và tích hợp hệ thống.

- **Clean Architecture & Repository Pattern**  
  Kiến trúc tách biệt rõ ràng giữa các tầng (Controller, Service, Data), giúp code dễ bảo trì, mở rộng, kiểm thử và tái sử dụng.

- **Visual Studio 2022/VS Code**  
  Công cụ phát triển mạnh mẽ, hỗ trợ debug, quản lý project, tích hợp Git, tăng hiệu suất lập trình.

- **Git**  
  Hệ thống quản lý phiên bản phân tán, giúp lưu trữ lịch sử thay đổi, làm việc nhóm hiệu quả, dễ dàng rollback khi cần thiết.

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

## Nhật ký thực tập phát triển chức năng

### Tuần 1: Làm quen dự án & công nghệ
- **Ngày 1:** Nhận đề tài, tìm hiểu tổng quan về phòng khám thú y và yêu cầu dự án.
- **Ngày 2:** Cài đặt môi trường phát triển (Visual Studio, SQL Server, .NET 9.0 SDK).
- **Ngày 3:** Đọc tài liệu, phân tích cấu trúc thư mục, tìm hiểu các package sử dụng.
- **Ngày 4:** Chạy thử project mẫu, làm quen với Swagger UI và Postman.
- **Ngày 5:** Tìm hiểu về Entity Framework Core, JWT Authentication, Clean Architecture.

### Tuần 2: Chức năng quản lý người dùng
- **Ngày 6:** Thiết kế database cho bảng User, Customer, phân tích các trường dữ liệu cần thiết.
- **Ngày 7:** Xây dựng API đăng ký tài khoản khách hàng (`POST /api/user/register`).
- **Ngày 8:** Xây dựng API đăng nhập, trả về JWT Token (`POST /api/user/login`).
- **Ngày 9:** Thêm xác thực JWT cho các endpoint cần bảo vệ.
- **Ngày 10:** Xây dựng API lấy thông tin profile người dùng (`GET /api/user/profile`).

### Tuần 3: Chức năng quản lý thú cưng
- **Ngày 11:** Thiết kế bảng Pet, xây dựng model và migration.
- **Ngày 12:** Xây dựng API thêm thú cưng mới (`POST /api/pet`).
- **Ngày 13:** Xây dựng API lấy danh sách thú cưng của khách hàng (`GET /api/pet`).
- **Ngày 14:** Xây dựng API cập nhật, xóa thú cưng (`PUT`, `DELETE /api/pet/{id}`).
- **Ngày 15:** Thêm validation: không xóa thú cưng có lịch hẹn đang chờ/xác nhận, tính tuổi tự động.

### Tuần 4: Chức năng quản lý lịch hẹn
- **Ngày 16:** Thiết kế bảng Appointment, xây dựng migration.
- **Ngày 17:** Xây dựng API đặt lịch hẹn mới (`POST /api/appointment`).
- **Ngày 18:** Xây dựng API lấy danh sách, chi tiết lịch hẹn (`GET /api/appointment`, `/api/appointment/{id}`).
- **Ngày 19:** Xây dựng API hủy lịch hẹn (`DELETE /api/appointment/{id}`), kiểm tra trạng thái hợp lệ.
- **Ngày 20:** Thêm validation: không đặt lịch trong quá khứ, không trùng lịch, chỉ được hủy khi status = 0.

### Tuần 5: Chức năng quản lý bác sĩ, dịch vụ, tin tức
- **Ngày 21:** Thiết kế bảng Doctor, Service, News, tạo migration.
- **Ngày 22:** Xây dựng API danh sách bác sĩ (`GET /api/doctor`).
- **Ngày 23:** Xây dựng API danh sách dịch vụ, tìm kiếm, lọc (`GET /api/service`).
- **Ngày 24:** Xây dựng API danh sách tin tức, tìm kiếm, phân trang (`GET /api/news`).
- **Ngày 25:** Hoàn thiện các chức năng hỗ trợ cho khách hàng và quản trị viên.

### Tuần 6: Hoàn thiện, kiểm thử & viết tài liệu
- **Ngày 26:** Kiểm thử API bằng Swagger UI, Postman, sửa lỗi phát hiện được.
- **Ngày 27:** Viết tài liệu hướng dẫn sử dụng API, mô tả các endpoint, tham số, ví dụ request/response.
- **Ngày 28:** Tổng hợp kinh nghiệm, tối ưu code, bổ sung kiểm tra bảo mật, hoàn thiện báo cáo thực tập.

--- 