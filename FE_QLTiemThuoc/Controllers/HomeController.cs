using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FE_QLTiemThuoc.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("MyApi");

            // Gọi API và đọc toàn bộ response (vì có thêm "status", "message")
            var response = await client.GetFromJsonAsync<JsonElement>("Thuoc/TopLoaiThuoc");

            // Kiểm tra nếu có trường "data" thì lấy ra làm model
            if (response.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                var categories = new List<JsonElement>();
                foreach (var item in dataElement.EnumerateArray())
                {
                    categories.Add(item);
                }

                return View(categories); // Truyền List<JsonElement> xuống View
            }

            // Nếu không có data thì truyền null
            return View(null);
        }
    }
}
