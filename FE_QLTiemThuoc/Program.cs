using Microsoft.AspNetCore.StaticFiles;
using System;

var builder = WebApplication.CreateBuilder(args);

// Configure backend API base address. Use http port 5203 which is what the
// backend (BE_QLTiemThuoc) is listening on during development.
// If you prefer HTTPS, change to the appropriate port that the backend is
// actually listening on (see BE Properties/launchSettings.json).
builder.Services.AddHttpClient("MyApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7283/api/");
});

builder.Services.AddControllersWithViews();
// session support (store logged-in employee code)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Ensure .avif is served with correct MIME type
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".avif"] = "image/avif";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TaiKhoan}/{action=DangNhap}");

app.Run();
