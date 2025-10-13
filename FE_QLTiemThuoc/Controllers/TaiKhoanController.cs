using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using FE_QLTiemThuoc.Models;

namespace FE_QLTiemThuoc.Controllers
{
    public class TaiKhoanController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public TaiKhoanController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult DangNhap()
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DangNhap(string tenDangNhap, string matKhau)
        {
            if (string.IsNullOrWhiteSpace(tenDangNhap) || string.IsNullOrWhiteSpace(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            var client = _clientFactory.CreateClient("MyApi");

            var json = JsonSerializer.Serialize(new
            {
                TenDangNhap = tenDangNhap,
                MatKhau = matKhau
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("TaiKhoan/Login", content);

            if (response.IsSuccessStatusCode)
            {
                // Đăng nhập thành công → chuyển trang chính
                return RedirectToAction("Index", "Home");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ViewBag.Error = string.IsNullOrWhiteSpace(error)
                    ? "Sai tên đăng nhập hoặc mật khẩu"
                    : error;
                return View();
            }
        }

        [HttpGet]
        public IActionResult DangKy()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> DangKy(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var client = _clientFactory.CreateClient("MyApi");

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(new
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhau = model.MatKhau,
                    EMAIL = model.Email
                }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("TaiKhoan", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng kiểm tra email.";
                return RedirectToAction("DangNhap");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Lỗi đăng ký: " + error);
                return View(model);
            }
        }
    }
}
