using BE_QLTiemThuoc.Data;
using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using System;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration; // Biến này đã có sẵn thông qua builder

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

// Cấu hình CORS
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

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

// =========================================================
// !!! KHỐI CẤU HÌNH CLOUDINARY ĐÃ ĐƯỢC DI CHUYỂN LÊN TRƯỚC builder.Build() !!!
// =========================================================

// Khai báo Account và cấu hình Cloudinary
var account = new Account(
    configuration["Cloudinary:CloudName"], // Lấy giá trị của CloudName
    configuration["Cloudinary:ApiKey"],    // Lấy giá trị của ApiKey
    configuration["Cloudinary:ApiSecret"]  // Lấy giá trị của ApiSecret
);

// Đăng ký Cloudinary là Singleton Service (Phải nằm trong builder.Services...)
builder.Services.AddSingleton(new Cloudinary(account));

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