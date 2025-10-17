using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using System;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
/*
# chuyển vào thư mục project (tùy chọn)
Set-Location -Path "i:\Ky_06_2025_2026\KhoaLuan\DoAn\QLTiemThuoc\BE_QLTiemThuoc"
dotnet run --launch-profile "https"

Set-Location -Path "i:\Ky_06_2025_2026\KhoaLuan\DoAn\QLTiemThuoc\FE_QLTiemThuoc"
dotnet run --launch-profile "https"
*/
app.UseHttpsRedirection();
app.UseCors(MyAllowSpecificOrigins);
app.UseAuthorization();

app.MapControllers();
app.Run();