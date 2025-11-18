using BE_QLTiemThuoc.Data;
using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using System;
using BE_QLTiemThuoc.Repositories;
using BE_QLTiemThuoc.Services;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration; // Biến này đã có sẵn thông qua builder

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
Env.Load();
// Cấu hình CORS
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// Read Cloudinary settings directly from environment variables or fallback to IConfiguration
var cloudinaryCloudName = Environment.GetEnvironmentVariable("Cloudinary__CloudName") ?? configuration["Cloudinary:CloudName"];
var cloudinaryApiKey = Environment.GetEnvironmentVariable("Cloudinary__ApiKey") ?? configuration["Cloudinary:ApiKey"];
var cloudinaryApiSecret = Environment.GetEnvironmentVariable("Cloudinary__ApiSecret") ?? configuration["Cloudinary:ApiSecret"];

// Read PayOS settings directly from environment variables or fallback to IConfiguration
var payosClientId = Environment.GetEnvironmentVariable("PayOS__ClientId") ?? configuration["PayOS:ClientId"];
var payosApiKey = Environment.GetEnvironmentVariable("PayOS__ApiKey") ?? configuration["PayOS:ApiKey"];
var payosChecksumKey = Environment.GetEnvironmentVariable("PayOS__ChecksumKey") ?? configuration["PayOS:ChecksumKey"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClientFactory
builder.Services.AddHttpClient();

// Register Repositories and Services (concrete types only — no interfaces)
builder.Services.AddScoped<NhaCungCapRepository>();
builder.Services.AddScoped<NhaCungCapService>();
builder.Services.AddScoped<KhachHangRepository>();
builder.Services.AddScoped<KhachHangService>();
builder.Services.AddScoped<ImagesService>();
builder.Services.AddScoped<NhomLoaiRepository>();
builder.Services.AddScoped<NhomLoaiService>();
builder.Services.AddScoped<PhieuNhapRepository>();
builder.Services.AddScoped<PhieuNhapService>();
builder.Services.AddScoped<ThuocRepository>();
builder.Services.AddScoped<ThuocService>();
builder.Services.AddScoped<PhieuQuyDoiService>();
builder.Services.AddScoped<NhanVienRepository>();
builder.Services.AddScoped<NhanVienService>();

// =========================================================
// !!! KHỐI CẤU HÌNH CLOUDINARY ĐÃ ĐƯỢC DI CHUYỂN LÊN TRƯỚC builder.Build() !!!
// =========================================================

// Khai báo Account và cấu hình Cloudinary
var account = new Account(
    // Prefer environment-loaded values (via Env.Load()) with fallback to appsettings
    cloudinaryCloudName,
    cloudinaryApiKey,
    cloudinaryApiSecret
);

// Đăng ký Cloudinary là Singleton Service (Phải nằm trong builder.Services...)
builder.Services.AddSingleton(new Cloudinary(account));

// PayOS configuration will be handled via IConfiguration in PaymentController

// =========================================================

var app = builder.Build(); // Service collection bị khóa tại đây

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(MyAllowSpecificOrigins);
app.UseAuthorization();

app.MapControllers();
app.Run();