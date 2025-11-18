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
                    return (object)new LoaiThuocThongKe
                    {
                        MaLoaiThuoc = loai.MaLoaiThuoc,
                        TenLoaiThuoc = loai.TenLoaiThuoc,
                        Icon = loai.Icon,
                        SoLuongThuoc = thuocInfo?.SoLuongThuoc ?? 0
                    };
                })
                .OrderByDescending(x => ((LoaiThuocThongKe)x).SoLuongThuoc)
                .ToList();

            return thongKeList;
        }

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
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result);
        }
        // GET: list of Thuoc aggregated by available lots in TonKho (only lots with SoLuongCon>0 and TrangThaiSeal==0)
        // Only show GiaThuoc where TrangThai == true (active prices)
        public async Task<object> GetListThuocTonKhoAsync()
        {
            var ctx = _repo.Context;
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
                          tongSoLuongCon = g.TongSoLuongCon,
                          // include only active GiaThuoc rows (TrangThai == true) for this Thuoc and compute available quantity per MaLoaiDonVi
                          GiaThuocs = ctx.GiaThuocs
                                        .Where(x => x.MaThuoc == t.MaThuoc && x.TrangThai)
                                        .Select(x => new
                                        {
                                            x.MaGiaThuoc,
                                            x.MaLoaiDonVi,
                                            TenLoaiDonVi = ctx.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == x.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                                            x.SoLuong,
                                            x.DonGia,
                                            x.TrangThai,
                                            SoLuongCon = ctx.TonKhos.Where(tk => tk.MaThuoc == t.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal).Sum(tk => (int?)tk.SoLuongCon) ?? 0
                                        })
                                        .ToList()
                      })
                .Where(x => x.GiaThuocs.Count > 0)
                .OrderBy(x => x.tenThuoc)
                .ToListAsync();

            return result;
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
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    GiaThuocs = ctx.GiaThuocs
                                .Where(x => x.MaThuoc == t.MaThuoc)
                                .Select(x => new
                                {
                                    x.MaGiaThuoc,
                                    x.MaLoaiDonVi,
                                    TenLoaiDonVi = ctx.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == x.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                                    x.SoLuong,
                                    x.DonGia,
                                    x.TrangThai,
                                    SoLuongCon = ctx.TonKhos.Where(tk => tk.MaThuoc == t.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal).Sum(tk => (int?)tk.SoLuongCon) ?? 0
                                })
                                .ToList()
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
                    // price from GIATHUOC
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    // include only active GiaThuoc rows that also have available stock in TonKho
                    GiaThuocs = ctx.GiaThuocs
                                .Where(x => x.MaThuoc == t.MaThuoc && x.TrangThai
                                             && ctx.TonKhos.Any(tk => tk.MaThuoc == t.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal))
                                .Select(x => new
                                {
                                    x.MaGiaThuoc,
                                    x.MaLoaiDonVi,
                                    TenLoaiDonVi = ctx.Set<Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == x.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                                    x.SoLuong,
                                    x.DonGia,
                                    x.TrangThai,
                                    SoLuongCon = ctx.TonKhos.Where(tk => tk.MaThuoc == t.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal).Sum(tk => (int?)tk.SoLuongCon) ?? 0
                                })
                                .ToList()
                })
                // remove Thuoc entries that ended up with no available GiaThuocs
                .Where(t => t.GiaThuocs.Count > 0)
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
                    t.CongDung,
                    t.CachDung,
                    t.LuuY,
                    t.UrlAnh,
                    t.MaNCC,
                    TenNCC = ctx.NhaCungCaps.Where(n => n.MaNCC == t.MaNCC).Select(n => n.TenNCC).FirstOrDefault(),
                    GiaThuocs = ctx.GiaThuocs
                                .Where(x => x.MaThuoc == t.MaThuoc)
                                .Select(x => new
                                {
                                    x.MaGiaThuoc,
                                    x.MaLoaiDonVi,
                                    TenLoaiDonVi = ctx.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == x.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                                    x.SoLuong,
                                    x.DonGia,
                                    x.TrangThai,
                                    SoLuongCon = ctx.TonKhos.Where(tk => tk.MaThuoc == t.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal).Sum(tk => (int?)tk.SoLuongCon) ?? 0
                                })
                                .ToList()
                })
                .FirstOrDefaultAsync()
                .ContinueWith(t => (object?)t.Result);
        }

        // GET: all GiaThuoc rows for a given MaThuoc with computed SoLuongCon and TenLoaiDonVi
        public Task<object> GetGiaThuocsByMaThuocAsync(string maThuoc)
        {
            var ctx = _repo.Context;
            return ctx.GiaThuocs
                .Where(g => g.MaThuoc == maThuoc)
                .Select(x => new
                {
                    x.MaGiaThuoc,
                    x.MaLoaiDonVi,
                    TenLoaiDonVi = ctx.Set<LoaiDonVi>().Where(d => d.MaLoaiDonVi == x.MaLoaiDonVi).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                    x.SoLuong,
                    x.DonGia,
                    x.TrangThai,
                    SoLuongCon = ctx.TonKhos.Where(tk => tk.MaThuoc == x.MaThuoc && tk.MaLoaiDonViTinh == x.MaLoaiDonVi && tk.SoLuongCon > 0 && !tk.TrangThaiSeal).Sum(tk => (int?)tk.SoLuongCon) ?? 0
                })
                .ToListAsync()
                .ContinueWith(t => (object)t.Result!);
        }

        public Task<List<LoaiDonVi>> GetLoaiDonViAsync()
        {
            return _repo.Context.LoaiDonVi.ToListAsync();
        }

        public async Task<Thuoc> CreateThuocAsync(ThuocDto thuocDto, HttpRequest? request = null)
        {
            if (thuocDto == null) throw new ArgumentNullException(nameof(thuocDto));

            // Server generates a unique MaThuoc for new medicines (clients don't send MaThuoc)
            string newMaThuoc;
            do
            {
                newMaThuoc = "T" + Guid.NewGuid().ToString("N").Substring(0, 9).ToUpper();
            } while (await _repo.AnyByMaThuocAsync(newMaThuoc));

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

            if (thuocDto.FileAnh?.Length > 0)
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
                MaThuoc = newMaThuoc,
                MaLoaiThuoc = thuocDto?.MaLoaiThuoc ?? string.Empty,
                TenThuoc = thuocDto?.TenThuoc ?? string.Empty,
                ThanhPhan = thuocDto?.ThanhPhan ?? string.Empty,
                MoTa = thuocDto?.MoTa ?? string.Empty,
                CongDung = thuocDto?.CongDung ?? string.Empty,
                CachDung = thuocDto?.CachDung ?? string.Empty,
                LuuY = thuocDto?.LuuY ?? string.Empty,
                MaNCC = thuocDto?.MaNCC ?? string.Empty,
                UrlAnh = imageUrl ?? string.Empty
            };

            var ctx = _repo.Context;
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {
                await _repo.AddAsync(thuoc);
                await _repo.SaveChangesAsync();

                if (thuocDto.GiaThuocs?.Count > 0)
                {
                    var existingCount = await ctx.GiaThuocs.CountAsync(x => x.MaThuoc == thuoc.MaThuoc);
                    var nextIndex = existingCount + 1;

                    string BuildMaGiaThuoc(string baseMaThuoc, int idx)
                    {
                        // Use GUID to ensure global uniqueness, 10 chars total
                        return "GT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    }

                    foreach (var g in thuocDto.GiaThuocs)
                    {
                        var newMa = BuildMaGiaThuoc(newMaThuoc, nextIndex++);
                        var newRow = new GiaThuoc
                        {
                            MaGiaThuoc = newMa,
                            MaThuoc = newMaThuoc,
                            MaLoaiDonVi = g.MaLoaiDonVi,
                            SoLuong = g.SoLuong,
                            DonGia = g.DonGia,
                            TrangThai = g.TrangThai
                        };
                        await ctx.GiaThuocs.AddAsync(newRow);
                    }
                    await ctx.SaveChangesAsync();
                }

                await tx.CommitAsync();
                return thuoc;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // Update
        public async Task<bool> UpdateThuocAsync(string id, ThuocDto thuocDto, HttpRequest? request = null)
        {
            if (thuocDto == null) throw new ArgumentNullException(nameof(thuocDto));
            // DTO no longer contains MaThuoc; use route `id` as the identifier for updates.

            var entity = await _repo.FindAsync(id);
            if (entity == null) throw new Exception("Không tìm thấy thuốc.");

            // Handle file upload similar to Create
            if (thuocDto.FileAnh?.Length > 0)
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
            // Note: unit/pricing moved to GIATHUOC; Thuoc table does not store MaLoaiDonVi/SoLuong
            entity.CongDung = thuocDto.CongDung ?? entity.CongDung;
            entity.CachDung = thuocDto.CachDung ?? entity.CachDung;
            entity.LuuY = thuocDto.LuuY ?? entity.LuuY;
            entity.MaNCC = thuocDto.MaNCC ?? entity.MaNCC;
            // Pricing lives in GIATHUOC now; update prices via separate endpoints for GIATHUOC

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

            // Update/create GIATHUOC rows if provided
            var ctx = _repo.Context;
            if (thuocDto.GiaThuocs?.Any() == true)
            {
                // if any incoming price row is active, deactivate existing rows first
                if (thuocDto.GiaThuocs.Any(x => x.TrangThai))
                {
                    var existingActive = ctx.GiaThuocs.Where(x => x.MaThuoc == entity.MaThuoc && x.TrangThai);
                    await existingActive.ForEachAsync(x => x.TrangThai = false);
                }

                // compute existing count once to avoid duplicate MaGiaThuoc when adding multiple new rows
                var existingCount2 = await ctx.GiaThuocs.CountAsync(x => x.MaThuoc == entity.MaThuoc);
                var nextIndex2 = existingCount2 + 1;

                // helper to build MaGiaThuoc in the format GT{NNN}/{index}
                string BuildMaGiaThuoc2(string baseMaThuoc, int idx)
                {
                    // Use GUID to ensure global uniqueness, 10 chars total
                    return "GT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                }

                foreach (var g in thuocDto.GiaThuocs)
                {
                    // Try to match an existing GiaThuoc by MaLoaiDonVi + SoLuong; if found update, otherwise create new
                    var existing = await ctx.GiaThuocs.FirstOrDefaultAsync(x =>
                        x.MaThuoc == entity.MaThuoc &&
                        x.MaLoaiDonVi == g.MaLoaiDonVi &&
                        x.SoLuong == g.SoLuong);

                    if (existing != null)
                    {
                        existing.MaLoaiDonVi = g.MaLoaiDonVi;
                        existing.SoLuong = g.SoLuong;
                        existing.DonGia = g.DonGia;
                        existing.TrangThai = g.TrangThai;
                        continue;
                    }

                    // Avoid adding duplicates if identical row already exists
                    var isDuplicate = await ctx.GiaThuocs.AnyAsync(x =>
                        x.MaThuoc == entity.MaThuoc &&
                        x.MaLoaiDonVi == g.MaLoaiDonVi &&
                        x.SoLuong == g.SoLuong);

                    if (isDuplicate)
                        continue;

                    var newMa = BuildMaGiaThuoc2(entity.MaThuoc, nextIndex2++);
                    var newRow = new GiaThuoc
                    {
                        MaGiaThuoc = newMa,
                        MaThuoc = entity.MaThuoc,
                        MaLoaiDonVi = g.MaLoaiDonVi,
                        SoLuong = g.SoLuong,
                        DonGia = g.DonGia,
                        TrangThai = g.TrangThai
                    };
                    await ctx.GiaThuocs.AddAsync(newRow);
                }

                await ctx.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteThuocAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id is required");

            var ctx = _repo.Context;
            var entity = await _repo.FindAsync(id);
            if (entity == null) throw new Exception("Không tìm thấy thuốc.");

            // Use a transaction so we don't partially delete GiaThuoc when Thuoc is in use
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {
                // Check references that would prevent deletion
                var usedInTonKho = await ctx.TonKhos.AnyAsync(tk => tk.MaThuoc == id);
                var usedInChiTietPhieuNhap = await ctx.ChiTietPhieuNhaps.AnyAsync(ct => ct.MaThuoc == id);

                // check ChiTietHoaDon via linking to TonKho.MaLo
                var usedInChiTietHoaDon = await (from ct in ctx.ChiTietHoaDons
                                                  join tk in ctx.TonKhos on ct.MaLo equals tk.MaLo
                                                  where tk.MaThuoc == id
                                                  select ct).AnyAsync();

                if (usedInTonKho || usedInChiTietPhieuNhap || usedInChiTietHoaDon)
                {
                    // don't delete anything, return informative error
                    throw new Exception("Thuốc đang được sử dụng và không thể xóa.");
                }

                // safe to delete related GiaThuoc rows first
                var giaRows = await ctx.GiaThuocs.Where(g => g.MaThuoc == id).ToListAsync();
                if (giaRows.Any()) ctx.GiaThuocs.RemoveRange(giaRows);

                // remove Thuoc
                _repo.Remove(entity);

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
