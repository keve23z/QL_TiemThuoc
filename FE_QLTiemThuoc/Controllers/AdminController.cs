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
                model.UrlAnh = $"assets_user/img/produc/{model.UrlAnh}";
                var addResponse = await client.PostAsJsonAsync("Thuoc", model);
                if (!addResponse.IsSuccessStatusCode)
                    ModelState.AddModelError("", "Thêm thuốc thất bại!");
            }

            // Xử lý chỉnh sửa
            if (actionType == "edit" && model != null && ModelState.IsValid)
            {
                model.UrlAnh = $"assets_user/img/produc/{model.UrlAnh}";
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
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<Models.NhaCungCap>>(dataElement.GetRawText(), options);
                        nccList = result ?? new List<Models.NhaCungCap>();
                    }
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<Models.NhaCungCap>>(json, options);
                        nccList = result ?? new List<Models.NhaCungCap>();
                    }
                    else
                    {
                        // Try to deserialize root element directly
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<Models.NhaCungCap>>(doc.RootElement.GetRawText(), options);
                        nccList = result ?? new List<Models.NhaCungCap>();
                    }
                }
                catch (Exception)
                {
                    nccList = new List<Models.NhaCungCap>();
                }
            }
            
            ViewBag.NCCList = nccList;
            return View();
        }        public IActionResult Index()
        {
            return View();
        }

        // Nhập thuốc view (filter form + list)
        [HttpGet]
        public IActionResult NhapThuoc()
        {
            // provide default date range for the view: first day of current month -> today
            var today = System.DateTime.Now;
            var firstOfMonth = new System.DateTime(today.Year, today.Month, 1);
            ViewBag.DefaultFrom = firstOfMonth.ToString("yyyy-MM-dd");
            ViewBag.DefaultTo = today.ToString("yyyy-MM-dd");
            // expose API base address (as configured in Program.cs) so client-side JS can call backend
            try
            {
                var client = _http.CreateClient("MyApi");
                ViewBag.ApiBase = client.BaseAddress?.ToString() ?? "/api";
            }
            catch
            {
                ViewBag.ApiBase = "/api";
            }

            // expose current logged-in employee code (if any) so the view can auto-fill MaNV
            try
            {
                var maNV = HttpContext.Session.GetString("MaNhanVien");
                var tenNV = HttpContext.Session.GetString("TenNhanVien");
                ViewBag.MaNV = maNV ?? string.Empty;
                ViewBag.TenNV = tenNV ?? string.Empty;
            }
            catch
            {
                ViewBag.MaNV = string.Empty;
                ViewBag.TenNV = string.Empty;
            }

            // default datetime-local value for inline add form
            ViewBag.DefaultNgayNhap = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm");

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
        public IActionResult AddPhieuNhap()
        {
            // provide ApiBase for client JS if needed
            try
            {
                var client = _http.CreateClient("MyApi");
                ViewBag.ApiBase = client.BaseAddress?.ToString() ?? "/api";
            }
            catch
            {
                ViewBag.ApiBase = "/api";
            }

            // prepare an empty model with today's date
            var model = new FE_QLTiemThuoc.Models.PhieuNhapDto { NgayNhap = System.DateTime.Now };

            // Fetch supplier list to populate the select in the view
            try
            {
                var client = _http.CreateClient("MyApi");
                var listResponse = client.GetAsync("NhaCungCap").GetAwaiter().GetResult();
                if (listResponse.IsSuccessStatusCode)
                {
                    var json = listResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var result = System.Text.Json.JsonSerializer.Deserialize<List<FE_QLTiemThuoc.Models.NhaCungCap>>(dataElement.GetRawText(), options);
                            ViewBag.NCCList = result ?? new List<FE_QLTiemThuoc.Models.NhaCungCap>();
                        }
                        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var result = System.Text.Json.JsonSerializer.Deserialize<List<FE_QLTiemThuoc.Models.NhaCungCap>>(json, options);
                            ViewBag.NCCList = result ?? new List<FE_QLTiemThuoc.Models.NhaCungCap>();
                        }
                        else
                        {
                            ViewBag.NCCList = new List<FE_QLTiemThuoc.Models.NhaCungCap>();
                        }
                    }
                    catch
                    {
                        ViewBag.NCCList = new List<FE_QLTiemThuoc.Models.NhaCungCap>();
                    }
                }
                else
                {
                    ViewBag.NCCList = new List<FE_QLTiemThuoc.Models.NhaCungCap>();
                }
            }
            catch
            {
                ViewBag.NCCList = new List<FE_QLTiemThuoc.Models.NhaCungCap>();
            }

            try
            {
                var maNV = HttpContext.Session.GetString("MaNhanVien");
                var tenNV = HttpContext.Session.GetString("TenNhanVien");
                ViewBag.MaNV = maNV ?? string.Empty;
                ViewBag.TenNV = tenNV ?? string.Empty;
            }
            catch
            {
                ViewBag.MaNV = string.Empty;
                ViewBag.TenNV = string.Empty;
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddPhieuNhap(FE_QLTiemThuoc.Models.PhieuNhapDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var client = _http.CreateClient("MyApi");
            var resp = await client.PostAsJsonAsync("PhieuNhap/AddPhieuNhap", model);
            if (resp.IsSuccessStatusCode)
            {
                // optionally show success and redirect back to list
                return RedirectToAction("NhapThuoc");
            }

            // read error and show
            var text = await resp.Content.ReadAsStringAsync();
            ModelState.AddModelError("", "Lỗi khi gọi API: " + resp.StatusCode + " - " + text);
            return View(model);
        }
    }
}
