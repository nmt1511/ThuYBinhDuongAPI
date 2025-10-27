using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ThuYBinhDuongAPI.Models;
using ThuYBinhDuongAPI.Services;
using CloudinaryDotNet;

var builder = WebApplication.CreateBuilder(args);

// Thêm cấu hình từ appsettings.Local.json nếu tồn tại
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Thêm các services vào container
builder.Services.AddControllers();

// Cấu hình file upload size limit (10MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// Thêm Entity Framework cho cơ sở dữ liệu
builder.Services.AddDbContext<ThuybinhduongContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Thêm JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Thêm Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Thêm Notification Service (Local notification approach)
builder.Services.AddScoped<INotificationService, NotificationService>();

// Thêm Reminder Service
builder.Services.AddScoped<IReminderService, ReminderService>();

// Cấu hình Cloudinary
var cloudinarySettings = builder.Configuration.GetSection("Cloudinary");
var cloudinaryAccount = new Account(
    cloudinarySettings["CloudName"],
    cloudinarySettings["ApiKey"],
    cloudinarySettings["ApiSecret"]
);
builder.Services.AddSingleton(new Cloudinary(cloudinaryAccount));

// Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Thêm Swagger/OpenAPI để tạo tài liệu API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ThuYBinhDuong Veterinary Clinic API",
        Version = "v1",
        Description = "API quản lý phòng khám thú y ThuYBinhDuong",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "ThuYBinhDuong Clinic",
            Email = "info@thuybinhduong.com"
        }
    });

    // Thêm cấu hình JWT Bearer cho Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header sử dụng Bearer scheme. Nhập 'Bearer' [space] và JWT token của bạn.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Thêm CORS nếu cần cho frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// Cấu hình HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Chỉ hiển thị Swagger trong môi trường phát triển
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ThuYBinhDuong API v1");
        c.RoutePrefix = "swagger"; // Truy cập tại /swagger
        c.DocumentTitle = "ThuYBinhDuong - API Documentation";
    });
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// Kích hoạt CORS
app.UseCors("AllowAll");

// Kích hoạt Authentication và Authorization
app.UseAuthentication();
app.UseAuthorization();

// Ánh xạ các controllers
app.MapControllers();

app.Run();
