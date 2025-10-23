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
                // Try to read response body and detect whether the account has an employee code (MaNhanVien)
                var respText = await response.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(respText) ? "{}" : respText);
                    var root = doc.RootElement;
                    JsonElement dataEl = default;
                    bool hasData = false;
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d))
                    {
                        dataEl = d; hasData = true;
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        dataEl = root; hasData = true;
                    }

                    string? maNhanVien = null;
                    string? tenNhanVien = null;
                    if (hasData && dataEl.ValueKind == JsonValueKind.Object)
                    {
                        // look for common property name variants
                        string[] candidates = new[] { "MaNhanVien", "maNhanVien", "manhanvien", "MaNV", "maNV" };
                        foreach (var c in candidates)
                        {
                            if (dataEl.TryGetProperty(c, out var prop))
                            {
                                if (prop.ValueKind == JsonValueKind.String) maNhanVien = prop.GetString();
                                else maNhanVien = prop.ToString();
                                break;
                            }
                        }
                        // also try case-insensitive search through properties
                        if (string.IsNullOrEmpty(maNhanVien))
                        {
                            foreach (var p in dataEl.EnumerateObject())
                            {
                                if (string.Equals(p.Name, "manhanvien", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, "manv", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (p.Value.ValueKind == JsonValueKind.String) maNhanVien = p.Value.GetString(); else maNhanVien = p.Value.ToString();
                                    break;
                                }
                            }
                        }
                        // try to find a display name for the employee (hoten, TenNV, name, fullName)
                        if (string.IsNullOrEmpty(tenNhanVien))
                        {
                            string[] nameCandidates = new[] { "HoTen", "hoten", "TenNV", "tenNV", "TenNhanVien", "tenNhanVien", "name", "fullName", "FullName" };
                            foreach (var c in nameCandidates)
                            {
                                if (dataEl.TryGetProperty(c, out var prop))
                                {
                                    if (prop.ValueKind == JsonValueKind.String) { tenNhanVien = prop.GetString(); break; }
                                }
                            }

                            // fallback: case-insensitive property search for common names
                            if (string.IsNullOrEmpty(tenNhanVien))
                            {
                                foreach (var p in dataEl.EnumerateObject())
                                {
                                    if (string.Equals(p.Name, "tennhanvien", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(p.Name, "tennhanvien", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(p.Name, "tennv", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(p.Name, "hoten", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(p.Name, "hoten", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (p.Value.ValueKind == JsonValueKind.String) { tenNhanVien = p.Value.GetString(); break; }
                                    }
                                }
                            }
                        }
                        // also check if there's a nested 'NhanVien' object with HoTen (case-insensitive)
                        if (string.IsNullOrEmpty(tenNhanVien) && dataEl.TryGetProperty("NhanVien", out var nvObj) && nvObj.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var p in nvObj.EnumerateObject())
                            {
                                if (p.Value.ValueKind == JsonValueKind.String)
                                {
                                    var nameKey = p.Name ?? string.Empty;
                                    if (string.Equals(nameKey, "hoten", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(nameKey, "hoTen", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(nameKey, "tennhanvien", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(nameKey, "tennv", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(nameKey, "name", StringComparison.OrdinalIgnoreCase))
                                    {
                                        tenNhanVien = p.Value.GetString();
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(maNhanVien))
                    {
                        // Employee account -> save employee code in session then redirect to admin ThemThuoc
                        try
                        {
                            HttpContext.Session.SetString("MaNhanVien", maNhanVien);
                            if (!string.IsNullOrEmpty(tenNhanVien))
                                HttpContext.Session.SetString("TenNhanVien", tenNhanVien);
                        }
                        catch
                        {
                            // ignore if session not available
                        }
                        return RedirectToAction("ThemThuoc", "Admin");
                    }
                }
                catch
                {
                    // If parsing fails, ignore and fall back to normal redirect
                }

                // Default redirect for normal users
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
