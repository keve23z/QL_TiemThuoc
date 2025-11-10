using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Repositories;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BE_QLTiemThuoc.Services
{
    public class ThuocService
    {
        private readonly ThuocRepository _repo;
        private readonly Cloudinary _cloudinary;
        private readonly IWebHostEnvironment _env;

        public ThuocService(ThuocRepository repo, Cloudinary cloudinary, IWebHostEnvironment env)
        {
            _repo = repo;
            _cloudinary = cloudinary;
            _env = env;
        }

        private static string? ExtractFileNameFromUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            try
            {
                var s = input.Trim();
                if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(s);
                    s = uri.LocalPath ?? s;
                }
                var noQuery = s.Split('?')[0];
                noQuery = noQuery.Trim('/');
                var file = Path.GetFileName(noQuery);
                return string.IsNullOrEmpty(file) ? null : file;
            }
            catch
            {
                try
                {
                    var t = input.Split('?')[0].Trim('/');
                    var f = Path.GetFileName(t);
                    return string.IsNullOrEmpty(f) ? null : f;
                }
                catch
                {
                    return null;
                }
            }
        }

    public async Task<object> GetTopLoaiThuocAsync()
        {
            var ctx = _repo.Context;
            var thuocGroup = await ctx.Thuoc
                .GroupBy(t => t.MaLoaiThuoc)
                .Select(g => new
                {
                    MaLoaiThuoc = g.Key,
                    SoLuongThuoc = g.Count()
                })
                .ToListAsync();

            var loaiThuocList = await ctx.LoaiThuoc.ToListAsync();

            var thongKeList = loaiThuocList
                .Select(loai =>
                {
                    var thuocInfo = thuocGroup.FirstOrDefault(x => x.MaLoaiThuoc == loai.MaLoaiThuoc);
                    return (object)new Model.Thuoc.LoaiThuocThongKe
                    {
                        MaLoaiThuoc = loai.MaLoaiThuoc,
                        TenLoaiThuoc = loai.TenLoaiThuoc,
                        Icon = loai.Icon,
                        SoLuongThuoc = thuocInfo?.SoLuongThuoc ?? 0
                    };
                })
                .OrderByDescending(x => ((Model.Thuoc.LoaiThuocThongKe)x).SoLuongThuoc)
                .ToList();

            return (object)thongKeList;
        }

        public Task<List<Model.Thuoc.LoaiThuoc>> GetLoaiThuocAsync()
            => _repo.Context.LoaiThuoc.ToListAsync();

        public Task<object> GetThuocAsync()
        {
            return _repo.Context.Thuoc
                .Select(t => new
                {
                    t.MaThuoc,
                    t.MaLoaiThuoc,
                    t.TenThuoc,
                    t.MoTa,
                    t.UrlAnh,
                    t.DonGiaSi
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result);
        }

        public Task<object> GetListThuocDetailAsync()
        {
            var ctx = _repo.Context;
            return ctx.Thuoc
                .Select(t => new
                {
                    t.MaThuoc,
                    t.MaLoaiThuoc,
                    t.TenThuoc,
                    t.ThanhPhan,
                    t.MoTa,
                    t.MaLoaiDonVi,
                    t.SoLuong,
                    t.CongDung,
                    t.CachDung,
                    t.LuuY,
                    t.UrlAnh,
                    t.MaNCC,
                    t.DonGiaSi,
                    t.DonGiaLe,
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    TenLoaiDonVi = ctx.Set<Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result);
        }

        // GET: list of Thuoc aggregated by available lots in TonKho (only lots with SoLuongCon>0 and TrangThaiSeal==0)
        public async Task<object> GetListThuocTonKhoAsync()
        {
            var ctx = _repo.Context;

            // Group TonKhos by MaThuoc to compute remaining quantities, then join to Thuoc to get details
            var grouped = ctx.TonKhos
                .Where(tk => tk.SoLuongCon > 0 && !tk.TrangThaiSeal)
                .GroupBy(tk => tk.MaThuoc)
                .Select(g => new
                {
                    MaThuoc = g.Key,
                    TongSoLuongCon = g.Sum(tk => tk.SoLuongCon)
                });

            var result = await grouped
                .Join(ctx.Thuoc,
                      g => g.MaThuoc,
                      t => t.MaThuoc,
                      (g, t) => new
                      {
                          maThuoc = t.MaThuoc,
                          maLoaiThuoc = t.MaLoaiThuoc,
                          tenThuoc = t.TenThuoc,
                          thanhPhan = t.ThanhPhan,
                          moTa = t.MoTa,
                          urlAnh = t.UrlAnh,
                          donGiaSi = t.DonGiaSi,
                          tongSoLuongCon = g.TongSoLuongCon
                      })
                .OrderBy(x => x.tenThuoc)
                .ToListAsync();

            return (object)result;
        }

        public Task<object> GetThuocByLoaiAsync(string maLoaiThuoc)
        {
            var ctx = _repo.Context;
            return ctx.Thuoc
                .Where(t => t.MaLoaiThuoc == maLoaiThuoc)
                .Select(t => new
                {
                    t.MaThuoc,
                    t.MaLoaiThuoc,
                    t.TenThuoc,
                    t.MoTa,
                    t.UrlAnh,
                    t.DonGiaSi,
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    TenLoaiDonVi = ctx.Set<Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result);
        }

        // GET: api/Thuoc/ByLoaiTonKho/{maLoaiThuoc}
        // Return Thuoc of a given MaLoai that have available stock in TonKho (SoLuongCon>0 and not sealed)
        public Task<object> GetThuocByLoaiTonKhoAsync(string maLoaiThuoc)
        {
            var ctx = _repo.Context;

            return ctx.Thuoc
                .Where(t => t.MaLoaiThuoc == maLoaiThuoc
                            && ctx.TonKhos.Any(tk => tk.MaThuoc == t.MaThuoc && tk.SoLuongCon > 0 && !tk.TrangThaiSeal))
                .Select(t => new
                {
                    t.MaThuoc,
                    t.MaLoaiThuoc,
                    t.TenThuoc,
                    t.MoTa,
                    t.UrlAnh,
                    t.DonGiaSi,
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    TenLoaiDonVi = ctx.Set<Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result);
        }

        public Task<object?> GetThuocByIdAsync(string maThuoc)
        {
            var ctx = _repo.Context;
            return ctx.Thuoc
                .Where(t => t.MaThuoc == maThuoc)
                .Select(t => new
                {
                    t.MaThuoc,
                    t.MaLoaiThuoc,
                    t.TenThuoc,
                    t.ThanhPhan,
                    t.MoTa,
                    t.MaLoaiDonVi,
                    t.SoLuong,
                    t.CongDung,
                    t.CachDung,
                    t.LuuY,
                    t.UrlAnh,
                    t.MaNCC,
                    t.DonGiaSi,
                    t.DonGiaLe,
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    TenLoaiDonVi = ctx.Set<Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == t.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault()
                })
                .FirstOrDefaultAsync()
                .ContinueWith(t => (object?)t.Result);
        }

        public Task<List<Model.Thuoc.LoaiDonVi>> GetLoaiDonViAsync()
        {
            return _repo.Context.LoaiDonVi.ToListAsync();
        }

        // Create
        public async Task<Thuoc> CreateThuocAsync(ThuocDto thuocDto, HttpRequest? request = null)
        {
            if (thuocDto == null) throw new ArgumentNullException(nameof(thuocDto));

            if (await _repo.AnyByMaThuocAsync(thuocDto.MaThuoc))
                throw new Exception("Mã thuốc đã tồn tại.");

            string? imageUrl = null;
            string? providedUrlFromForm = null;
            try
            {
                if (request != null && request.HasFormContentType)
                {
                    var form = await request.ReadFormAsync();
                    if (thuocDto != null && string.IsNullOrWhiteSpace(thuocDto.UrlAnh) && form.TryGetValue("UrlAnh", out var v))
                        providedUrlFromForm = v.ToString();
                    if (string.IsNullOrWhiteSpace(providedUrlFromForm))
                    {
                        var foundKey = form.Keys.FirstOrDefault(k => string.Equals(k, "urlanh", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(foundKey)) providedUrlFromForm = form[foundKey].ToString();
                    }
                }
            }
            catch { }

            if (thuocDto.FileAnh != null && thuocDto.FileAnh.Length > 0)
            {
                if (thuocDto.FileAnh.Length > 5242880) throw new Exception("Kích thước file ảnh không được vượt quá 5MB.");

                var tagsList = new List<string> { "thuoc" };
                if (!string.IsNullOrEmpty(thuocDto.MaLoaiThuoc)) tagsList.Add(thuocDto.MaLoaiThuoc.Trim().ToLower());
                string tagsString = string.Join(",", tagsList);

                using (var stream = thuocDto.FileAnh.OpenReadStream())
                {
                    var originalFileName = Path.GetFileName(thuocDto.FileAnh.FileName);
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName) ?? "file";
                    var ext = Path.GetExtension(originalFileName) ?? "";
                    string SanitizeForPublicId(string s) => Regex.Replace(s, @"[^A-Za-z0-9_\-]", "_");

                    var baseCandidate = SanitizeForPublicId(nameWithoutExt);
                    string candidateStored = baseCandidate + ext;
                    string candidatePublicBase = baseCandidate;
                    int suffix = 1;
                    while (await _repo.Context.Thuoc.AnyAsync(t => t.UrlAnh.ToLower() == candidateStored.ToLower()))
                    {
                        candidatePublicBase = SanitizeForPublicId($"{baseCandidate}_{suffix}");
                        candidateStored = candidatePublicBase + ext;
                        suffix++;
                    }

                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(thuocDto.FileAnh.FileName, stream),
                        Folder = "thuoc_images",
                        PublicId = $"thuoc/{candidatePublicBase}",
                        Overwrite = false,
                        Tags = tagsString
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                        throw new Exception($"Lỗi Cloudinary: {uploadResult.Error?.Message}");

                    string finalFileName;
                    if (!string.IsNullOrEmpty(uploadResult.PublicId))
                    {
                        var lastSegment = uploadResult.PublicId.Contains('/') ? uploadResult.PublicId.Split('/').Last() : uploadResult.PublicId;
                        finalFileName = !string.IsNullOrEmpty(uploadResult.Format) ? lastSegment + "." + uploadResult.Format : candidateStored;
                    }
                    else if (uploadResult.SecureUrl != null)
                    {
                        finalFileName = Path.GetFileName(new Uri(uploadResult.SecureUrl.ToString()).LocalPath);
                    }
                    else
                    {
                        finalFileName = candidateStored;
                    }

                    imageUrl = finalFileName;
                }
            }
            else
            {
                var urlToUse = !string.IsNullOrWhiteSpace(thuocDto?.UrlAnh) ? thuocDto.UrlAnh : providedUrlFromForm;
                if (!string.IsNullOrWhiteSpace(urlToUse))
                {
                    // If client provided an absolute URL, keep it as-is (do not extract filename)
                    if (urlToUse.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || urlToUse.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        imageUrl = urlToUse;
                    }
                    else
                    {
                        var extracted = ExtractFileNameFromUrl(urlToUse);
                        imageUrl = extracted ?? urlToUse;
                    }
                }
            }

            var thuoc = new Thuoc
            {
                MaThuoc = thuocDto?.MaThuoc ?? string.Empty,
                MaLoaiThuoc = thuocDto?.MaLoaiThuoc ?? string.Empty,
                TenThuoc = thuocDto?.TenThuoc ?? string.Empty,
                ThanhPhan = thuocDto?.ThanhPhan ?? string.Empty,
                MoTa = thuocDto?.MoTa ?? string.Empty,
                MaLoaiDonVi = thuocDto?.MaLoaiDonVi ?? string.Empty,
                SoLuong = thuocDto?.SoLuong ?? 0,
                CongDung = thuocDto?.CongDung ?? string.Empty,
                CachDung = thuocDto?.CachDung ?? string.Empty,
                LuuY = thuocDto?.LuuY ?? string.Empty,
                MaNCC = thuocDto?.MaNCC ?? string.Empty,
                DonGiaSi = thuocDto?.DonGiaSi ?? 0,
                DonGiaLe = thuocDto?.DonGiaLe ?? 0,
                UrlAnh = imageUrl ?? string.Empty
            };

            await _repo.AddAsync(thuoc);
            await _repo.SaveChangesAsync();

            return thuoc;
        }

        // Update
        public async Task<bool> UpdateThuocAsync(string id, ThuocDto thuocDto, HttpRequest? request = null)
        {
            if (thuocDto == null) throw new ArgumentNullException(nameof(thuocDto));
            if (id != thuocDto.MaThuoc) throw new Exception("Mã thuốc không khớp.");

            var entity = await _repo.FindAsync(id);
            if (entity == null) throw new Exception("Không tìm thấy thuốc.");

            // Handle file upload similar to Create
            if (thuocDto.FileAnh != null && thuocDto.FileAnh.Length > 0)
            {
                if (thuocDto.FileAnh.Length > 5242880) throw new Exception("Kích thước file ảnh không được vượt quá 5MB.");

                var tagsList = new List<string> { "thuoc" };
                if (!string.IsNullOrEmpty(thuocDto.MaLoaiThuoc)) tagsList.Add(thuocDto.MaLoaiThuoc.Trim().ToLower());
                string tagsString = string.Join(",", tagsList);

                using (var stream = thuocDto.FileAnh.OpenReadStream())
                {
                    var originalFileName = Path.GetFileName(thuocDto.FileAnh.FileName);
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName) ?? "file";
                    var ext = Path.GetExtension(originalFileName) ?? "";
                    string SanitizeForPublicId(string s) => Regex.Replace(s, @"[^A-Za-z0-9_\-]", "_");

                    var baseCandidate = SanitizeForPublicId(nameWithoutExt);
                    string candidateStored = baseCandidate + ext;
                    string candidatePublicBase = baseCandidate;
                    int suffix = 1;
                    while (await _repo.Context.Thuoc.AnyAsync(t => t.UrlAnh.ToLower() == candidateStored.ToLower()))
                    {
                        candidatePublicBase = SanitizeForPublicId($"{baseCandidate}_{suffix}");
                        candidateStored = candidatePublicBase + ext;
                        suffix++;
                    }

                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(thuocDto.FileAnh.FileName, stream),
                        Folder = "thuoc_images",
                        PublicId = $"thuoc/{candidatePublicBase}",
                        Overwrite = false,
                        Tags = tagsString
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                        throw new Exception($"Lỗi Cloudinary: {uploadResult.Error?.Message}");

                    string finalFileName;
                    if (!string.IsNullOrEmpty(uploadResult.PublicId))
                    {
                        var lastSegment = uploadResult.PublicId.Contains('/') ? uploadResult.PublicId.Split('/').Last() : uploadResult.PublicId;
                        finalFileName = !string.IsNullOrEmpty(uploadResult.Format) ? lastSegment + "." + uploadResult.Format : candidateStored;
                    }
                    else if (uploadResult.SecureUrl != null)
                    {
                        finalFileName = Path.GetFileName(new Uri(uploadResult.SecureUrl.ToString()).LocalPath);
                    }
                    else
                    {
                        finalFileName = candidateStored;
                    }

                    entity.UrlAnh = finalFileName;

                    // Copy to FE wwwroot (best-effort)
                    try
                    {
                        var beContentRoot = _env?.ContentRootPath;
                        if (!string.IsNullOrEmpty(beContentRoot))
                        {
                            var solutionDir = Directory.GetParent(beContentRoot)?.FullName;
                            if (!string.IsNullOrEmpty(solutionDir))
                            {
                                var feTargetDir = Path.Combine(solutionDir, "FE_QLTiemThuoc", "wwwroot", "assets_user", "img", "product");
                                if (!Directory.Exists(feTargetDir)) Directory.CreateDirectory(feTargetDir);
                                var feTargetPath = Path.Combine(feTargetDir, finalFileName);
                                try { if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin); } catch { }
                                using (var fs2 = new FileStream(feTargetPath, FileMode.Create, FileAccess.Write))
                                {
                                    stream.CopyTo(fs2);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(thuocDto.UrlAnh))
                {
                    // Preserve absolute URLs provided by client
                    if (thuocDto.UrlAnh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || thuocDto.UrlAnh.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.UrlAnh = thuocDto.UrlAnh;
                    }
                    else
                    {
                        var extracted = ExtractFileNameFromUrl(thuocDto.UrlAnh);
                        entity.UrlAnh = extracted ?? thuocDto.UrlAnh;
                    }
                }
            }

            entity.MaLoaiThuoc = thuocDto.MaLoaiThuoc ?? entity.MaLoaiThuoc;
            entity.TenThuoc = thuocDto.TenThuoc ?? entity.TenThuoc;
            entity.ThanhPhan = thuocDto.ThanhPhan ?? entity.ThanhPhan;
            entity.MoTa = thuocDto.MoTa ?? entity.MoTa;
            entity.MaLoaiDonVi = thuocDto.MaLoaiDonVi ?? entity.MaLoaiDonVi;
            entity.SoLuong = thuocDto.SoLuong ?? entity.SoLuong;
            entity.CongDung = thuocDto.CongDung ?? entity.CongDung;
            entity.CachDung = thuocDto.CachDung ?? entity.CachDung;
            entity.LuuY = thuocDto.LuuY ?? entity.LuuY;
            entity.MaNCC = thuocDto.MaNCC ?? entity.MaNCC;
            entity.DonGiaSi = thuocDto.DonGiaSi ?? entity.DonGiaSi;
            entity.DonGiaLe = thuocDto.DonGiaLe ?? entity.DonGiaLe;

            try
            {
                if ((thuocDto.FileAnh == null || thuocDto.FileAnh.Length == 0) && !string.IsNullOrWhiteSpace(thuocDto.UrlAnh))
                {
                    // If UrlAnh is absolute, preserve it. Otherwise try to extract the filename.
                    if (thuocDto.UrlAnh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || thuocDto.UrlAnh.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.UrlAnh = thuocDto.UrlAnh;
                    }
                    else
                    {
                        var extracted2 = ExtractFileNameFromUrl(thuocDto.UrlAnh);
                        entity.UrlAnh = extracted2 ?? thuocDto.UrlAnh;
                    }
                }
            }
            catch { }

            await _repo.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteThuocAsync(string id)
        {
            var entity = await _repo.FindAsync(id);
            if (entity == null) throw new Exception("Không tìm thấy thuốc.");
            _repo.Remove(entity);
            await _repo.SaveChangesAsync();
            return true;
        }
    }
}
