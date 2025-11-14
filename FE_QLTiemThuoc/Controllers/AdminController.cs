using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FE_QLTiemThuoc.Models; // Đảm bảo có ThuocViewModel

namespace FE_QLTiemThuoc.Controllers
{
    public class AdminController : Controller
    {
        private readonly IHttpClientFactory _http;

        public AdminController(IHttpClientFactory http)//https://localhost:7283/api/
        {
            _http = http;
        }

        // Xử lý tất cả trên 1 view: hiển thị danh sách, thêm, chỉnh sửa
        [HttpGet, HttpPost]
        public async Task<IActionResult> ThemThuoc(ThuocViewModel model = null, string editId = null, string actionType = null)
        {
            var client = _http.CreateClient("MyApi");
            // Sửa đoạn lấy danh sách thuốc để lấy đúng dữ liệu từ API
            var listResponse = await client.GetAsync("Thuoc/ListThuocDetail");
            List<ThuocViewModel>? thuocList = null;
            if (listResponse.IsSuccessStatusCode)
            {
                var json = await listResponse.Content.ReadAsStringAsync();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(dataElement.GetRawText());
                    }
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(json);
                    }
                    else
                    {
                        // Nếu không phải object hoặc array, thử deserialize trực tiếp
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(json);
                    }
                }
                catch
                {
                    thuocList = new List<ThuocViewModel>();
                }
            }
            else
            {
                thuocList = new List<ThuocViewModel>();
            }

            // Đọc JSON trả về từ API, lấy trường "data" (nếu có) rồi deserialize
            var loaiResponse = await client.GetAsync("Thuoc/LoaiThuoc");
            List<LoaiThuoc> loaiList = new();
            if (loaiResponse.IsSuccessStatusCode)
            {
                var json = await loaiResponse.Content.ReadAsStringAsync();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        loaiList = System.Text.Json.JsonSerializer.Deserialize<List<LoaiThuoc>>(dataElement.GetRawText());
                    }
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        loaiList = System.Text.Json.JsonSerializer.Deserialize<List<LoaiThuoc>>(json);
                    }
                }
                catch
                {
                    loaiList = new List<LoaiThuoc>();
                }
            }
            ViewBag.LoaiList = loaiList;

            // Xử lý thêm mới
            if (actionType == "add" && model != null && ModelState.IsValid)
            {
                // Sinh mã thuốc tự động nếu chưa có
                if (string.IsNullOrEmpty(model.MaThuoc))
                {
                    int maxNumber = 0;
                    if (thuocList != null && thuocList.Count > 0)
                    {
                        // Tìm số lớn nhất trong các mã thuốc hiện có (dạng T001, T002,...)
                        foreach (var t in thuocList)
                        {
                            if (!string.IsNullOrEmpty(t.MaThuoc) && t.MaThuoc.Length > 1)
                            {
                                if (int.TryParse(t.MaThuoc.Substring(1), out int num))
                                {
                                    if (num > maxNumber)
                                        maxNumber = num;
                                }
                            }
                        }
                    }
                    int nextNumber = maxNumber + 1;
                    model.MaThuoc = $"T{nextNumber.ToString("D3")}";
                }
                // If UrlAnh is an absolute URL (starts with http/https or is already a path), don't prefix it.
                if (!string.IsNullOrEmpty(model.UrlAnh) &&
                    !(model.UrlAnh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      model.UrlAnh.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      model.UrlAnh.StartsWith("/") ||
                      model.UrlAnh.Contains("assets_user")))
                {
                    model.UrlAnh = $"assets_user/img/produc/{model.UrlAnh}";
                }
                var addResponse = await client.PostAsJsonAsync("Thuoc", model);
                if (!addResponse.IsSuccessStatusCode)
                    ModelState.AddModelError("", "Thêm thuốc thất bại!");
            }

            // Xử lý chỉnh sửa
            if (actionType == "edit" && model != null && ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(model.UrlAnh) &&
                    !(model.UrlAnh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      model.UrlAnh.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      model.UrlAnh.StartsWith("/") ||
                      model.UrlAnh.Contains("assets_user")))
                {
                    model.UrlAnh = $"assets_user/img/produc/{model.UrlAnh}";
                }
                var editResponse = await client.PutAsJsonAsync($"Thuoc/{model.MaThuoc}", model); // Renamed variable
                if (!editResponse.IsSuccessStatusCode)
                    ModelState.AddModelError("", "Cập nhật thuốc thất bại!");
            }

            // Nếu có editId, lấy thông tin thuốc để đổ lên form
            ThuocViewModel editThuoc = null;
            if (!string.IsNullOrEmpty(editId))
                editThuoc = await client.GetFromJsonAsync<ThuocViewModel>($"Thuoc/{editId}");

            // Truyền cả danh sách và model chỉnh sửa sang view
            ViewBag.ThuocList = thuocList;
            ViewBag.EditThuoc = editThuoc;
            return View();
        }

        // Quản lý nhà cung cấp
        [HttpGet, HttpPost]
        public async Task<IActionResult> QuanLyNCC(string? actionType = null, string? editId = null)
        {
            var client = _http.CreateClient("MyApi");
            
            // Lấy danh sách nhà cung cấp
            var listResponse = await client.GetAsync("NhaCungCap");
            var nccList = new List<Models.NhaCungCap>();
            if (listResponse.IsSuccessStatusCode)
            {
                var json = await listResponse.Content.ReadAsStringAsync();
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(dataElement.GetRawText(), options);
                        nccList = result ?? new List<NhaCungCap>();
                    }
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(json, options);
                        nccList = result ?? new List<NhaCungCap>();
                    }
                    else
                    {
                        // Try to deserialize root element directly
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(doc.RootElement.GetRawText(), options);
                        nccList = result ?? new List<NhaCungCap>();
                    }
                }
                catch (Exception)
                {
                    nccList = new List<NhaCungCap>();
                }
            }
            
            ViewBag.NCCList = nccList;
            return View();
        }        public IActionResult Index()
        {
            return View();
        }

       

        // Debug: return current session-stored employee info
        [HttpGet]
        public IActionResult SessionInfo()
        {
            try
            {
                var maNV = HttpContext.Session.GetString("MaNhanVien") ?? string.Empty;
                var tenNV = HttpContext.Session.GetString("TenNhanVien") ?? string.Empty;
                return Json(new { MaNhanVien = maNV, TenNhanVien = tenNV });
            }
            catch
            {
                return Json(new { MaNhanVien = string.Empty, TenNhanVien = string.Empty });
            }
        }
        [HttpGet]
        public async Task<IActionResult> NhapThuocList()
        {
            // Api base for client-side fetch
            try
            {
                var c0 = _http.CreateClient("MyApi");
                ViewBag.ApiBase = c0.BaseAddress?.ToString() ?? "/api";
            }
            catch { ViewBag.ApiBase = "/api"; }

            // Preload NCC and Thuoc list (optional but improves UX and offline resilience)
            var client = _http.CreateClient("MyApi");

            // NCC list
            try
            {
                var r = await client.GetAsync("NhaCungCap");
                var nccList = new List<NhaCungCap>();
                if (r.IsSuccessStatusCode)
                {
                    var json = await r.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions{ PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        nccList = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(dataEl.GetRawText(), opt) ?? new();
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        nccList = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(json, opt) ?? new();
                }
                ViewBag.NCCList = nccList;
            }
            catch { ViewBag.NCCList = new List<NhaCungCap>(); }

            // Thuoc list (rich)
            try
            {
                var r2 = await client.GetAsync("Thuoc/ListThuocDetail");
                var thuocList = new List<ThuocViewModel>();
                if (r2.IsSuccessStatusCode)
                {
                    var json = await r2.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions{ PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(dataEl.GetRawText(), opt) ?? new();
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(json, opt) ?? new();
                }
                ViewBag.ThuocList = thuocList;
            }
            catch { ViewBag.ThuocList = new List<ThuocViewModel>(); }

            // Employee info and defaults
            try
            {
                ViewBag.MaNV = HttpContext.Session.GetString("MaNhanVien") ?? string.Empty;
                ViewBag.TenNV = HttpContext.Session.GetString("TenNhanVien") ?? string.Empty;
            }
            catch { ViewBag.MaNV = string.Empty; ViewBag.TenNV = string.Empty; }

            var today = DateTime.Now.Date;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);
            ViewBag.DefaultFrom = firstOfMonth.ToString("yyyy-MM-dd");
            ViewBag.DefaultTo = today.ToString("yyyy-MM-dd");

            ViewBag.DefaultNgayNhap = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
            return View();
        }
        
        // Danh sách hoá đơn (Admin) - gọi BE api/HoaDon/Search
        [HttpGet]
        public async Task<IActionResult> HoaDonList(string? from = null, string? to = null, int? status = null, string? q = null, string? loai = null)
        {
            var client = _http.CreateClient("MyApi");

            // defaults: first of month -> today
            var today = DateTime.Now.Date;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);
            var fromStr = from ?? firstOfMonth.ToString("yyyy-MM-dd");
            var toStr = to ?? today.ToString("yyyy-MM-dd");

            try
            {
                var url = $"HoaDon/Search?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}" + (status != null ? $"&status={status.Value}" : string.Empty) + (!string.IsNullOrEmpty(q) ? $"&q={Uri.EscapeDataString(q)}" : string.Empty) + (!string.IsNullOrEmpty(loai) ? $"&loai={Uri.EscapeDataString(loai)}" : string.Empty);
                var res = await client.GetAsync(url);
                var list = new List<System.Collections.Generic.Dictionary<string, object>>();
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    try
                    {
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            list = System.Text.Json.JsonSerializer.Deserialize<List<System.Collections.Generic.Dictionary<string, object>>>(dataEl.GetRawText(), options) ?? new();
                        }
                        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            list = System.Text.Json.JsonSerializer.Deserialize<List<System.Collections.Generic.Dictionary<string, object>>>(json, options) ?? new();
                        }
                    }
                    catch { list = new(); }
                }
                ViewBag.HoaDonList = list;
            }
            catch
            {
                ViewBag.HoaDonList = new List<System.Collections.Generic.Dictionary<string, object>>();
            }

            ViewBag.DefaultFrom = fromStr;
            ViewBag.DefaultTo = toStr;
            ViewBag.Status = status?.ToString() ?? string.Empty;
            ViewBag.Query = q ?? string.Empty;
            ViewBag.Loai = loai ?? string.Empty;

            // API base (so client-side can call backend directly when FE and BE run on different ports)
            try
            {
                ViewBag.ApiBase = client.BaseAddress?.ToString()?.TrimEnd('/') ?? "/api";
            }
            catch
            {
                ViewBag.ApiBase = "/api";
            }

            return View();
        }
        
        // Kho - Thuốc: hiển thị danh sách Chưa tách lẻ / Đã tách lẻ
        [HttpGet]
        public IActionResult ThuocKho()
        {
            try
            {
                var c0 = _http.CreateClient("MyApi");
                var raw = c0.BaseAddress?.ToString() ?? "/api";
                // normalize: remove trailing slash and remove trailing '/api' if present
                var norm = raw.TrimEnd('/');
                if (norm.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    norm = norm.Substring(0, norm.Length - 4); // remove '/api'
                }
                ViewBag.ApiBase = norm;
            }
            catch { ViewBag.ApiBase = "/api"; }

            try
            {
                ViewBag.MaNV = HttpContext.Session.GetString("MaNhanVien") ?? string.Empty;
                ViewBag.TenNV = HttpContext.Session.GetString("TenNhanVien") ?? string.Empty;
            }
            catch { ViewBag.MaNV = string.Empty; ViewBag.TenNV = string.Empty; }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AddPhieuNhap()
        {
            // Api base for client-side fetch
            try
            {
                var c0 = _http.CreateClient("MyApi");
                ViewBag.ApiBase = c0.BaseAddress?.ToString() ?? "/api";
            }
            catch { ViewBag.ApiBase = "/api"; }

            // Preload NCC and Thuoc list (optional but improves UX and offline resilience)
            var client = _http.CreateClient("MyApi");

            // NCC list
            try
            {
                var r = await client.GetAsync("NhaCungCap");
                var nccList = new List<NhaCungCap>();
                if (r.IsSuccessStatusCode)
                {
                    var json = await r.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions{ PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        nccList = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(dataEl.GetRawText(), opt) ?? new();
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        nccList = System.Text.Json.JsonSerializer.Deserialize<List<NhaCungCap>>(json, opt) ?? new();
                }
                ViewBag.NCCList = nccList;
            }
            catch { ViewBag.NCCList = new List<NhaCungCap>(); }

            // Thuoc list (rich)
            try
            {
                var r2 = await client.GetAsync("Thuoc/ListThuocDetail");
                var thuocList = new List<ThuocViewModel>();
                if (r2.IsSuccessStatusCode)
                {
                    var json = await r2.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions{ PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(dataEl.GetRawText(), opt) ?? new();
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        thuocList = System.Text.Json.JsonSerializer.Deserialize<List<ThuocViewModel>>(json, opt) ?? new();
                }
                ViewBag.ThuocList = thuocList;
            }
            catch { ViewBag.ThuocList = new List<ThuocViewModel>(); }

            // Employee info
            try
            {
                ViewBag.MaNV = HttpContext.Session.GetString("MaNhanVien") ?? string.Empty;
                ViewBag.TenNV = HttpContext.Session.GetString("TenNhanVien") ?? string.Empty;
            }
            catch { ViewBag.MaNV = string.Empty; ViewBag.TenNV = string.Empty; }

            ViewBag.DefaultNgayNhap = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
            return View();
        }
        
        // GET: Admin/ThemHoaDon - show form to create a new invoice
        [HttpGet]
        public async Task<IActionResult> ThemHoaDon()
        {
            var client = _http.CreateClient("MyApi");

            // API base for client-side fetch
            try { ViewBag.ApiBase = client.BaseAddress?.ToString().TrimEnd('/') ?? "/api"; } catch { ViewBag.ApiBase = "/api"; }

            // Preload available lots from ThuocView/ChuaTachLe and ThuocView/DaTachLe
            try
            {
                var r2 = await client.GetAsync("ThuocView/ChuaTachLe");
                var lotListChua = new List<object>();
                if (r2.IsSuccessStatusCode)
                {
                    var json = await r2.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        lotListChua = System.Text.Json.JsonSerializer.Deserialize<List<object>>(json, opt) ?? new();
                    else if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        lotListChua = System.Text.Json.JsonSerializer.Deserialize<List<object>>(dataEl.GetRawText(), opt) ?? new();
                }
                ViewBag.LotListChua = lotListChua;
            }
            catch { ViewBag.LotListChua = new List<object>(); }

            try
            {
                var r3 = await client.GetAsync("ThuocView/DaTachLe");
                var lotListDa = new List<object>();
                if (r3.IsSuccessStatusCode)
                {
                    var json = await r3.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        lotListDa = System.Text.Json.JsonSerializer.Deserialize<List<object>>(json, opt) ?? new();
                    else if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        lotListDa = System.Text.Json.JsonSerializer.Deserialize<List<object>>(dataEl.GetRawText(), opt) ?? new();
                }
                ViewBag.LotListDa = lotListDa;
            }
            catch { ViewBag.LotListDa = new List<object>(); }

            // Preload KhachHang list
            try
            {
                var r = await client.GetAsync("KhachHang");
                var khList = new List<Models.KhachHangViewModel>();
                if (r.IsSuccessStatusCode)
                {
                    var json = await r.Content.ReadAsStringAsync();
                    var opt = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        khList = System.Text.Json.JsonSerializer.Deserialize<List<Models.KhachHangViewModel>>(dataEl.GetRawText(), opt) ?? new();
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        khList = System.Text.Json.JsonSerializer.Deserialize<List<Models.KhachHangViewModel>>(json, opt) ?? new();
                }
                ViewBag.KhachHangList = khList;
            }
            catch { ViewBag.KhachHangList = new List<Models.KhachHangViewModel>(); }

            // Employee info
            try
            {
                ViewBag.MaNV = HttpContext.Session.GetString("MaNhanVien") ?? string.Empty;
                ViewBag.TenNV = HttpContext.Session.GetString("TenNhanVien") ?? string.Empty;
            }
            catch { ViewBag.MaNV = string.Empty; ViewBag.TenNV = string.Empty; }

            ViewBag.DefaultNgayLap = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
            return View();
        }
        
    }
}
