using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FE_QLTiemThuoc.Controllers
{
    public class UserController : Controller
    {
        private readonly IHttpClientFactory _http;

        public UserController(IHttpClientFactory http)
        {
            _http = http;
        }

        public async Task<IActionResult> ShopGrid(string? search, string? category, string? sort)
        {
            var client = _http.CreateClient("MyApi");

            //  1. Lấy danh mục loại thuốc cho sidebar
            var categoryResponse = await client.GetFromJsonAsync<JsonElement>("Thuoc/TopLoaiThuoc");
            if (categoryResponse.TryGetProperty("data", out var catData) && catData.ValueKind == JsonValueKind.Array)
            {
                ViewBag.Categories = catData.EnumerateArray().ToList();
            }

            //  2. Lấy danh sách thuốc theo category (nếu có)
            string? apiUrl;
            if (!string.IsNullOrEmpty(category))
            {
                apiUrl = $"Thuoc/ByLoai/{category}";
            }
            else if (!string.IsNullOrEmpty(search))
            {
                apiUrl = $"Thuoc/Search?keyword={search}";
            }
            else
            {
                apiUrl = $"Thuoc";
            }

            JsonElement thuocResponse = default;
            if (apiUrl != null)
            {
                thuocResponse = await client.GetFromJsonAsync<JsonElement>(apiUrl);
            }

            List<JsonElement>? products = null;
            if (thuocResponse.ValueKind == JsonValueKind.Object &&
    thuocResponse.TryGetProperty("data", out var dataElement) &&
    dataElement.ValueKind == JsonValueKind.Array)
            {
                products = dataElement.EnumerateArray().ToList();
            }


            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Sort = sort;

            return View(products);
        }


        public IActionResult About()
        {
            return View(); // Sẽ tự động tìm Views/User/About.cshtml
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
