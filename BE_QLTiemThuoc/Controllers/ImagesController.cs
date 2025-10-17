using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Http;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private static readonly HttpClient _http = new HttpClient();

        // Note: the images folder is inside the FE project wwwroot. We compute it relative to the BE project working directory.
        private string GetImagesFolder()
        {
            // BE project directory (where the app runs)
            var beRoot = Directory.GetCurrentDirectory();
            // Path to FE wwwroot assets folder (relative sibling)
            var candidate = Path.GetFullPath(Path.Combine(beRoot, "..", "FE_QLTiemThuoc", "wwwroot", "assets_user", "img", "product"));
            return candidate;
        }

        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var folder = GetImagesFolder();
                if (!Directory.Exists(folder)) return Array.Empty<string>();
                var files = Directory.GetFiles(folder)
                    .Select(f => Path.GetFileName(f))
                    .OrderBy(n => n)
                    .ToArray();
                return files;
            });

            return Ok(response);
        }

    public class ImportRequest { public string? url { get; set; } }

        [HttpPost("UploadExternal")]
        public async Task<IActionResult> UploadExternal([FromBody] ImportRequest req)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.url)) throw new Exception("Missing url");
                var uri = new Uri(req.url);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "imported-image" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".jpg";

                var folder = GetImagesFolder();
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // normalize filename and ensure uniqueness
                var safeName = SanitizeFileName(fileName);
                var target = Path.Combine(folder, safeName);
                var baseName = Path.GetFileNameWithoutExtension(safeName);
                var ext = Path.GetExtension(safeName);
                int counter = 1;
                while (System.IO.File.Exists(target))
                {
                    var newName = baseName + "_" + counter + ext;
                    target = Path.Combine(folder, newName);
                    counter++;
                }

                // download content
                var bytes = await _http.GetByteArrayAsync(req.url);
                await System.IO.File.WriteAllBytesAsync(target, bytes);

                return Path.GetFileName(target);
            });

            return Ok(response);
        }

    [HttpPost("UploadFile")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (file == null) throw new Exception("File missing");

                var folder = GetImagesFolder();
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var safeName = SanitizeFileName(file.FileName ?? ("upload_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".dat"));
                var target = Path.Combine(folder, safeName);
                var baseName = Path.GetFileNameWithoutExtension(safeName);
                var ext = Path.GetExtension(safeName);
                int counter = 1;
                while (System.IO.File.Exists(target))
                {
                    var newName = baseName + "_" + counter + ext;
                    target = Path.Combine(folder, newName);
                    counter++;
                }

                using (var stream = System.IO.File.Create(target))
                {
                    await file.CopyToAsync(stream);
                }

                return Path.GetFileName(target);
            });

            return Ok(response);
        }

        // basic filename sanitizer
        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
