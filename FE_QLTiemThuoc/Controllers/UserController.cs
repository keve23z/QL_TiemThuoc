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

        // GET: /User/ItemDetail?maThuoc=T001  (or route configured to /User/ItemDetail/T001)
        public async Task<IActionResult> ItemDetail(string? maThuoc)
        {
            if (string.IsNullOrEmpty(maThuoc)) return BadRequest("maThuoc is required");

            var client = _http.CreateClient("MyApi");

            try
            {
                // call api Thuoc/{maThuoc}
                var resp = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"Thuoc/{maThuoc}");
                if (resp.ValueKind == System.Text.Json.JsonValueKind.Object && resp.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    // pass the inner data object to the view as model
                    return View(data);
                }

                // if response doesn't have data, pass empty model and show message in view
                ViewBag.Error = "Không tìm thấy dữ liệu thuốc.";
                return View(new System.Text.Json.JsonElement());
            }
            catch (System.Exception ex)
            {
                // log later if needed; show friendly message
                ViewBag.Error = "Lỗi khi lấy dữ liệu từ API: " + ex.Message;
                return View(new System.Text.Json.JsonElement());
            }
        }

        public async Task<IActionResult> ShopGrid(string? search, string? category, string? sort, string? view)
        {
            var client = _http.CreateClient("MyApi");

            //  1. Lấy danh mục loại thuốc cho sidebar
            var categoryResponse = await client.GetFromJsonAsync<JsonElement>("Thuoc/TopLoaiThuoc");
            if (categoryResponse.TryGetProperty("data", out var catData) && catData.ValueKind == JsonValueKind.Array)
            {
                ViewBag.Categories = catData.EnumerateArray().ToList();
            }

            //  2. Lấy danh sách thuốc theo category (nếu có)
            // Use TonKho-aware endpoints so frontend shows only items with available stock
            string? apiUrl;
            if (!string.IsNullOrEmpty(category))
            {
                // Use aggregated-by-category endpoint that only returns items with TonKho > 0
                apiUrl = $"Thuoc/ByLoaiTonKho/{category}";
            }
            else if (!string.IsNullOrEmpty(search))
            {
                // Keep search as-is (search endpoint remains unchanged)
                apiUrl = $"Thuoc/Search?keyword={search}";
            }
            else
            {
                // Use aggregated list that returns all Thuoc with available stock
                apiUrl = $"Thuoc/ListThuocTonKho";
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

            // Support server-side sorting for price
            if (products != null && !string.IsNullOrEmpty(sort))
            {
                if (sort == "price_asc")
                {
                    products = products.OrderBy(p => p.TryGetProperty("donGiaSi", out var priceProp) && priceProp.ValueKind == JsonValueKind.Number ? priceProp.GetDecimal() : decimal.MaxValue).ToList();
                }
                else if (sort == "price_desc")
                {
                    products = products.OrderByDescending(p => p.TryGetProperty("donGiaSi", out var priceProp) && priceProp.ValueKind == JsonValueKind.Number ? priceProp.GetDecimal() : decimal.MinValue).ToList();
                }
            }


            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Sort = sort;
            ViewBag.View = view; // "grid" or "list"

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
