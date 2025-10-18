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

        public AdminController(IHttpClientFactory http)
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
    }
}
