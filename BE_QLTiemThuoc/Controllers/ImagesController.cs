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

        // Temp upload folder (outside wwwroot to avoid triggering hot reload during development)
        private string GetTempUploadFolder()
        {
            var beRoot = Directory.GetCurrentDirectory();
            var candidate = Path.GetFullPath(Path.Combine(beRoot, "..", "TempImages"));
            if (!Directory.Exists(candidate))
                Directory.CreateDirectory(candidate);
            return candidate;
        }

        // Final product images folder (inside FE wwwroot)
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

    // Wrapper for file uploads so Swagger can describe the multipart/form-data schema
    public class UploadFileRequest
    {
        public IFormFile? file { get; set; }
    }

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
    public async Task<IActionResult> UploadFile([FromForm] UploadFileRequest req)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
        var file = req?.file;
        if (file == null) throw new Exception("File missing");

                // Upload to TEMP folder first (not wwwroot, so no hot reload)
                var folder = GetTempUploadFolder();
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

        // Move file from temp to product folder (called when user clicks Save)
        [HttpPost("FinalizeImage")]
        public async Task<IActionResult> FinalizeImage([FromBody] dynamic req)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var fileName = req?.fileName as string;
                if (string.IsNullOrEmpty(fileName)) throw new Exception("fileName required");

                var tempFolder = GetTempUploadFolder();
                var tempPath = Path.Combine(tempFolder, fileName);
                if (!System.IO.File.Exists(tempPath))
                    throw new Exception("File not found in temp folder");

                var finalFolder = GetImagesFolder();
                if (!Directory.Exists(finalFolder)) Directory.CreateDirectory(finalFolder);
                var finalPath = Path.Combine(finalFolder, fileName);

                // If file already exists in final folder, overwrite or rename
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                int counter = 1;
                while (System.IO.File.Exists(finalPath))
                {
                    var newName = baseName + "_" + counter + ext;
                    finalPath = Path.Combine(finalFolder, newName);
                    counter++;
                }

                // Copy from temp to final
                System.IO.File.Copy(tempPath, finalPath, overwrite: true);
                // Delete temp file
                System.IO.File.Delete(tempPath);

                return Path.GetFileName(finalPath);
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
