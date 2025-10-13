using FE_QLTiemThuoc.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace FE_QLTiemThuoc.Controllers
{
    public class KhachHangController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public KhachHangController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult DangKyThongTin()
        {
            return View(new KhachHangViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> DangKyThongTin(KhachHangViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var client = _clientFactory.CreateClient("MyApi");

            var json = JsonSerializer.Serialize(new
            {
                HoTen = model.HoTen,
                NgaySinh = model.NgaySinh,
                DienThoai = model.DienThoai,
                GioiTinh = model.GioiTinh,
                DiaChi = model.DiaChi
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("KhachHang", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Đăng ký thành công!";
                return RedirectToAction("ThanhCong");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Lỗi: " + error);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ThanhCong()
        {
            return View();
        }
    }
}
