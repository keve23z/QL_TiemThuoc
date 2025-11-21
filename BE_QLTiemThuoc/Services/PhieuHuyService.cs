using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuHuyService
    {
        private readonly PhieuHuyRepository _repo;

        public PhieuHuyService(PhieuHuyRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<object>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, bool? loaiHuy = null)
        {
            var phieuHuys = await _repo.GetByDateRangeAsync(startDate, endDate, loaiHuy);

            // collect MaNV to fetch names
            var maNvs = phieuHuys.Select(p => p.MaNV).Distinct().Where(m => !string.IsNullOrEmpty(m)).ToList();
            var nvMap = await _repo.GetNhanVienNamesAsync(maNvs);

            var result = phieuHuys.Select(p => new
            {
                p.MaPH,
                p.NgayHuy,
                LoaiHuy = p.LoaiHuy,
                LoaiHuyName = p.LoaiHuy == true ? "Hủy từ hóa đơn" : "Hủy từ kho",
                p.MaNV,
                NhanVienName = nvMap.ContainsKey(p.MaNV) ? nvMap[p.MaNV] : null,
                p.MaHD,
                p.TongMatHangHuy,
                p.TongSoLuongHuy,
                p.TongTienHuy,
                p.TongTienKho,
                p.TongTien,
                p.GhiChu,
                ChiTietCount = p.ChiTietPhieuHuys?.Count ?? 0
            }).ToList<object>();

            return result;
        }

        private async Task UpdateHoaDonStatusAfterHuyAsync(string maHD)
        {
            var hoaDon = await _repo.GetHoaDonByIdAsync(maHD);
            if (hoaDon == null) return;

            // Kiểm tra xem tất cả chi tiết hóa đơn đã được xử lý chưa
            // Giả sử có phương thức để kiểm tra trạng thái chi tiết
            var allChiTietProcessed = await CheckAllChiTietHoaDonProcessedAsync(maHD);

            if (allChiTietProcessed)
            {
                // Tất cả chi tiết đã xử lý - hoàn tất xử lý hủy
                hoaDon.TrangThaiGiaoHang = -2; // Hoàn tất xử lý hủy
            }
            else
            {
                // Còn chi tiết chưa xử lý hết
                hoaDon.TrangThaiGiaoHang = -3; // Còn chi tiết chưa xử lý hết
            }

            await _repo.UpdateHoaDonAsync(hoaDon);
        }

        private async Task<bool> CheckAllChiTietHoaDonProcessedAsync(string maHD)
        {
            // Lấy tổng số lượng trong hóa đơn
            var hoaDon = await _repo.GetHoaDonByIdAsync(maHD);
            if (hoaDon == null) return false;
            // Lấy trực tiếp các chi tiết hóa đơn và kiểm tra cờ TrangThaiXuLy
            var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(maHD);
            if (chiTietHoaDon == null || !chiTietHoaDon.Any()) return false;

            // Trả về true nếu mọi chi tiết đã có TrangThaiXuLy = true
            return chiTietHoaDon.All(ct => ct.TrangThaiXuLy == true);
        }

        private async Task<int> GetTongSoLuongHoaDonAsync(string maHD)
        {
            return await _repo.GetTongSoLuongHoaDonAsync(maHD);
        }

        private async Task<int> GetTongSoLuongDaHuyAsync(string maHD)
        {
            // Logic lấy tổng số lượng đã hủy từ các phiếu hủy liên quan
            var phieuHuys = await _repo.GetByDateRangeAsync(DateTime.MinValue, DateTime.MaxValue);
            var relatedPhieuHuys = phieuHuys.Where(p => p.LoaiHuy == true && p.MaHD == maHD).ToList();

            return relatedPhieuHuys.Sum(p => p.TongSoLuongHuy ?? 0);
        }

        public async Task<object> HuyHoaDonAsync(HuyHoaDonDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaHD))
            {
                throw new ArgumentException("MaHD is required");
            }

            if (dto.XuLyThuocs == null || !dto.XuLyThuocs.Any())
            {
                throw new ArgumentException("XuLyThuocs cannot be empty");
            }

            // Kiểm tra hóa đơn tồn tại
            var hoaDon = await _repo.GetHoaDonByIdAsync(dto.MaHD);
            if (hoaDon == null)
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHD} không tồn tại");
            }

            // Kiểm tra chi tiết hóa đơn và hạn sử dụng
            var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(dto.MaHD);
            if (chiTietHoaDon == null || !chiTietHoaDon.Any())
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHD} không có chi tiết");
            }

            // Lấy thông tin tồn kho cho các lô thuốc trong hóa đơn
            var maLoList = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo)).Select(ct => ct.MaLo!).Distinct().ToList();
            var tonKhoDict = new Dictionary<string, Model.Kho.TonKho>();
            if (maLoList.Any())
            {
                var tonKhos = await _repo.GetTonKhoByMaLoListAsync(maLoList);
                foreach (var tk in tonKhos)
                {
                    tonKhoDict[tk.MaLo] = tk;
                }
            }

            // Validate các lô thuốc trong request có thuộc về hóa đơn này không
            foreach (var xuLy in dto.XuLyThuocs)
            {
                var chiTiet = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == xuLy.MaLo);
                if (chiTiet == null)
                {
                    throw new ArgumentException($"Lô thuốc {xuLy.MaLo} không thuộc về hóa đơn {dto.MaHD}");
                }

                // Kiểm tra hạn sử dụng nếu cần hủy
                if (xuLy.LoaiXuLy == "HUY" && tonKhoDict.TryGetValue(xuLy.MaLo, out var tonKho) && tonKho.HanSuDung != null)
                {
                                        var ngayHetHan = tonKho.HanSuDung;
                                        var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;

                    // Nếu thuốc còn hạn > 6 tháng, cảnh báo nhưng vẫn cho phép hủy nếu có lý do đặc biệt
                    if (thangConLai > 6 && string.IsNullOrEmpty(xuLy.LyDoHuy))
                    {
                        throw new ArgumentException($"Thuốc {xuLy.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                    }
                }
            }

            var result = new
            {
                PhieuHuy = (object?)null,
                ThuocHoanLai = new List<object>(),
                TongSoLuongHuy = 0,
                TongSoLuongHoanLai = 0,
                Message = ""
            };

            // Tách thuốc cần hủy và thuốc có thể hoàn lại
            var thuocCanHuy = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HUY").ToList();
            var thuocHoanLai = dto.XuLyThuocs.Where(t => t.LoaiXuLy == "HOAN_LAI").ToList();

            // Xử lý thuốc hoàn lại vào kho
            var thuocHoanLaiDetails = new List<object>();
            foreach (var thuoc in thuocHoanLai)
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == thuoc.MaLo);
                if (chiTietHD != null && tonKhoDict.TryGetValue(thuoc.MaLo, out var tonKho))
                {
                    // Hoàn lại số lượng từ chi tiết hóa đơn vào kho
                    tonKho.SoLuongCon += chiTietHD.SoLuong;

                    await _repo.UpdateTonKhoAsync(tonKho);

                    thuocHoanLaiDetails.Add(new
                    {
                        thuoc.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        SoLuongHoanLai = chiTietHD.SoLuong,
                        thuoc.GhiChu
                    });
                }
            }

            // Xử lý thuốc cần hủy
            object? phieuHuyResult = null;
            if (thuocCanHuy.Any())
            {
                var phieuHuyDto = new PhieuHuyDto
                {
                    MaNV = dto.MaNV,
                    LoaiHuy = "HOADON",
                    MaHD = dto.MaHD,
                    GhiChu = dto.GhiChu,
                    ChiTietPhieuHuys = thuocCanHuy.Select(t =>
                    {
                        var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                        return new ChiTietPhieuHuyDto
                        {
                            MaLo = t.MaLo,
                            SoLuongHuy = t.SoLuongHuy ?? (chiTietHD?.SoLuong ?? 0),
                            LyDoHuy = t.LyDoHuy ?? "Hủy từ hóa đơn khách hàng",
                            GhiChu = t.GhiChu
                        };
                    }).ToList()
                };

                phieuHuyResult = await CreatePhieuHuyAsync(phieuHuyDto);

                // Nếu hủy theo hóa đơn thì đánh dấu các chi tiết hóa đơn tương ứng là đã xử lý (TrangThaiXuLy = true)
                try
                {
                    var processedMaLos2 = phieuHuyDto.ChiTietPhieuHuys.Select(x => x.MaLo).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
                    var affectedCthd2 = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo) && processedMaLos2.Contains(ct.MaLo)).ToList();
                    if (affectedCthd2.Any())
                    {
                        foreach (var c in affectedCthd2)
                        {
                            c.TrangThaiXuLy = true;
                        }
                        await _repo.UpdateChiTietHoaDonRangeAsync(affectedCthd2);
                    }
                }
                catch
                {
                    // swallow exceptions here to avoid breaking the main flow; consider logging
                }

                // Nếu hủy theo hóa đơn thì đánh dấu các chi tiết hóa đơn tương ứng là đã xử lý (TrangThaiXuLy = true)
                try
                {
                    var processedMaLos = phieuHuyDto.ChiTietPhieuHuys.Select(x => x.MaLo).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
                    var affectedCthd = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo) && processedMaLos.Contains(ct.MaLo)).ToList();
                    if (affectedCthd.Any())
                    {
                        foreach (var c in affectedCthd)
                        {
                            c.TrangThaiXuLy = true;
                        }
                        await _repo.UpdateChiTietHoaDonRangeAsync(affectedCthd);
                    }
                }
                catch
                {
                    // swallow exceptions here to avoid breaking the main flow; consider logging
                }
            }

            // Cập nhật trạng thái hóa đơn sau khi xử lý
            await UpdateHoaDonStatusAfterHuyAsync(dto.MaHD);

            var tongSoLuongHuy = thuocCanHuy.Sum(t => t.SoLuongHuy ?? 0);
            var tongSoLuongHoanLai = thuocHoanLai.Sum(t =>
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                return chiTietHD?.SoLuong ?? 0;
            });

            return new
            {
                PhieuHuy = phieuHuyResult,
                ThuocHoanLai = thuocHoanLaiDetails,
                TongSoLuongHuy = tongSoLuongHuy,
                TongSoLuongHoanLai = tongSoLuongHoanLai,
                Message = $"Đã xử lý hủy hóa đơn: hủy {tongSoLuongHuy} sản phẩm, hoàn lại {tongSoLuongHoanLai} sản phẩm vào kho"
            };
        }

        public async Task<object?> GetChiTietPhieuHuyAsync(string maPH)
        {
            var phieuHuy = await _repo.GetByIdAsync(maPH);
            if (phieuHuy == null) return null;

            // Fetch employee name
            var nvMap = await _repo.GetNhanVienNamesAsync(new List<string> { phieuHuy.MaNV });
            var nhanVienName = nvMap.ContainsKey(phieuHuy.MaNV) ? nvMap[phieuHuy.MaNV] : null;

            // Project phieu info
            var phieuInfo = new
            {
                phieuHuy.MaPH,
                phieuHuy.NgayHuy,
                phieuHuy.MaNV,
                NhanVienName = nhanVienName,
                phieuHuy.MaHD,
                phieuHuy.TongMatHangHuy,
                phieuHuy.TongSoLuongHuy,
                phieuHuy.TongTienHuy,
                phieuHuy.TongTienKho,
                phieuHuy.TongTien,
                phieuHuy.GhiChu,
                LoaiHuy = phieuHuy.LoaiHuy,
                LoaiHuyName = phieuHuy.LoaiHuy == true ? "Hủy từ hóa đơn" : "Hủy từ kho"
            };

            // Project detail items with extra fields
            var chiTiet = phieuHuy.ChiTietPhieuHuys.Select(ct => new
            {
                ct.MaCTPH,
                ct.MaLo,
                ct.SoLuongHuy,
                ct.DonGia,
                ct.ThanhTien,
                ct.LyDoHuy,
                ct.GhiChu,
                LoaiHuy = ct.LoaiHuy,
                LoaiHuyName = ct.LoaiHuy ? "Hủy vào kho" : "Hủy bình thường",
                TenThuoc = ct.TonKho?.Thuoc?.TenThuoc,
                HanSuDung = ct.TonKho?.HanSuDung,
                DonViTinh = ct.TonKho?.MaLoaiDonViTinh
            }).ToList();

            return new { PhieuHuy = phieuInfo, ChiTiet = chiTiet };
        }

        public async Task<object> HuyTuKhoAsync(HuyTuKhoDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaNhanVien))
            {
                throw new ArgumentException("MaNhanVien is required");
            }

            if (dto.ChiTietPhieuHuy == null || !dto.ChiTietPhieuHuy.Any())
            {
                throw new ArgumentException("ChiTietPhieuHuy cannot be empty");
            }

            // Validate tồn kho cho từng lô
            foreach (var chiTiet in dto.ChiTietPhieuHuy)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho == null)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} không tồn tại trong kho");
                }

                if (tonKho.SoLuongCon < chiTiet.SoLuongHuy)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} chỉ còn {tonKho.SoLuongCon} không đủ để hủy {chiTiet.SoLuongHuy}");
                }

                // Kiểm tra hạn sử dụng - ưu tiên hủy thuốc sắp hết hạn
                var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;
                if (thangConLai > 12 && string.IsNullOrEmpty(chiTiet.LyDoChiTiet))
                {
                    throw new ArgumentException($"Thuốc {chiTiet.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                }
            }

            // Tạo phiếu hủy từ kho
            var phieuHuyDto = new PhieuHuyDto
            {
                MaNV = dto.MaNhanVien,
                LoaiHuy = "KHO",
                MaHD = null,
                GhiChu = dto.LyDoHuy,
                ChiTietPhieuHuys = dto.ChiTietPhieuHuy.Select(c => new ChiTietPhieuHuyDto
                {
                    MaLo = c.MaLo,
                    SoLuongHuy = c.SoLuongHuy,
                    LyDoHuy = c.LyDoChiTiet,
                    GhiChu = c.LyDoChiTiet
                }).ToList()
            };

            var result = await CreatePhieuHuyAsync(phieuHuyDto);

            return new
            {
                MaPhieuHuy = ((dynamic)result).PhieuHuy?.MaPH,
                NgayHuy = ((dynamic)result).PhieuHuy?.NgayHuy,
                TongSoLuongHuy = dto.ChiTietPhieuHuy.Sum(c => c.SoLuongHuy),
                ChiTietPhieuHuy = dto.ChiTietPhieuHuy.Select(c =>
                {
                    var tonKho = _repo.GetTonKhoByMaLoAsync(c.MaLo).Result;
                    return new
                    {
                        c.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        c.SoLuongHuy,
                        DonGia = 0 // TonKho không có DonGia, có thể lấy từ ChiTietPhieuNhap sau
                    };
                }).ToList(),
                Message = "Tạo phiếu hủy từ kho thành công"
            };
        }

        public async Task<object> HuyTuHoaDonAsync(HuyTuHoaDonDto dto)
        {
            // Validation
            if (string.IsNullOrEmpty(dto.MaHoaDon))
            {
                throw new ArgumentException("MaHoaDon is required");
            }

            if (string.IsNullOrEmpty(dto.MaNhanVien))
            {
                throw new ArgumentException("MaNhanVien is required");
            }

            if (dto.ChiTietXuLy == null || !dto.ChiTietXuLy.Any())
            {
                throw new ArgumentException("ChiTietXuLy cannot be empty");
            }

            // Kiểm tra hóa đơn tồn tại
            var hoaDon = await _repo.GetHoaDonByIdAsync(dto.MaHoaDon);
            if (hoaDon == null)
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHoaDon} không tồn tại");
            }

            // Kiểm tra chi tiết hóa đơn
            var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(dto.MaHoaDon);
            if (chiTietHoaDon == null || !chiTietHoaDon.Any())
            {
                throw new ArgumentException($"Hóa đơn {dto.MaHoaDon} không có chi tiết");
            }

            // Lấy thông tin tồn kho cho các lô thuốc trong hóa đơn
            var maLoList = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo)).Select(ct => ct.MaLo!).Distinct().ToList();
            var tonKhoDict = new Dictionary<string, Model.Kho.TonKho>();
            if (maLoList.Any())
            {
                var tonKhos = await _repo.GetTonKhoByMaLoListAsync(maLoList);
                foreach (var tk in tonKhos)
                {
                    tonKhoDict[tk.MaLo] = tk;
                }
            }

            // Validate các lô thuốc trong request
            foreach (var xuLy in dto.ChiTietXuLy)
            {
                var chiTiet = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == xuLy.MaLo);
                if (chiTiet == null)
                {
                    throw new ArgumentException($"Lô thuốc {xuLy.MaLo} không thuộc về hóa đơn {dto.MaHoaDon}");
                }

                // Kiểm tra hạn sử dụng
                if (xuLy.LoaiXuLy == "HUY" && tonKhoDict.TryGetValue(xuLy.MaLo, out var tonKho))
                {
                    var thangConLai = (tonKho.HanSuDung - DateTime.Now).TotalDays / 30;
                    if (thangConLai > 6 && string.IsNullOrEmpty(xuLy.LyDoChiTiet))
                    {
                        throw new ArgumentException($"Thuốc {xuLy.MaLo} còn hạn {thangConLai:F1} tháng. Cần có lý do hủy cụ thể.");
                    }
                }
            }

            var thuocCanHuy = dto.ChiTietXuLy.Where(t => t.LoaiXuLy == "HUY").ToList();
            var thuocHoanLai = dto.ChiTietXuLy.Where(t => t.LoaiXuLy == "HOAN_LAI").ToList();

            // Xử lý hoàn lại kho
            var thuocHoanLaiDetails = new List<object>();
            foreach (var thuoc in thuocHoanLai)
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == thuoc.MaLo);
                if (chiTietHD != null && tonKhoDict.TryGetValue(thuoc.MaLo, out var tonKho))
                {
                    tonKho.SoLuongCon += chiTietHD.SoLuong;
                    await _repo.UpdateTonKhoAsync(tonKho);

                    thuocHoanLaiDetails.Add(new
                    {
                        thuoc.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        SoLuongHoanLai = chiTietHD.SoLuong,
                        thuoc.LyDoChiTiet
                    });
                }
            }

            // Xử lý hủy
            object? phieuHuyResult = null;
            if (thuocCanHuy.Any())
            {
                var phieuHuyDto = new PhieuHuyDto
                {
                    MaNV = dto.MaNhanVien,
                    LoaiHuy = "HOADON",
                    MaHD = dto.MaHoaDon,
                    GhiChu = dto.LyDoHuy,
                    ChiTietPhieuHuys = thuocCanHuy.Select(t =>
                    {
                        var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                        return new ChiTietPhieuHuyDto
                        {
                            MaLo = t.MaLo,
                            SoLuongHuy = t.SoLuongHuy ?? (chiTietHD?.SoLuong ?? 0),
                            LyDoHuy = t.LyDoChiTiet ?? "Hủy từ hóa đơn khách hàng",
                            GhiChu = t.LyDoChiTiet
                        };
                    }).ToList()
                };

                phieuHuyResult = await CreatePhieuHuyAsync(phieuHuyDto);
            }

            // Cập nhật trạng thái hóa đơn
            await UpdateHoaDonStatusAfterHuyAsync(dto.MaHoaDon);

            var tongSoLuongHuy = thuocCanHuy.Sum(t => t.SoLuongHuy ?? 0);
            var tongSoLuongHoanLai = thuocHoanLai.Sum(t =>
            {
                var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                return chiTietHD?.SoLuong ?? 0;
            });

            return new
            {
                MaPhieuHuy = ((dynamic)phieuHuyResult)?.PhieuHuy?.MaPH,
                MaHoaDon = dto.MaHoaDon,
                TrangThaiGiaoHang = hoaDon.TrangThaiGiaoHang,
                TongSoLuongHuy = tongSoLuongHuy,
                TongSoLuongHoanLai = tongSoLuongHoanLai,
                ChiTietXuLy = dto.ChiTietXuLy.Select(t =>
                {
                    var chiTietHD = chiTietHoaDon.FirstOrDefault(ct => ct.MaLo == t.MaLo);
                    tonKhoDict.TryGetValue(t.MaLo, out var tonKho);
                    return new
                    {
                        t.MaLo,
                        TenThuoc = tonKho?.Thuoc?.TenThuoc,
                        t.LoaiXuLy,
                        SoLuong = t.LoaiXuLy == "HUY" ? t.SoLuongHuy : chiTietHD?.SoLuong
                    };
                }).ToList(),
                Message = $"Xử lý hủy hóa đơn thành công: hủy {tongSoLuongHuy} sản phẩm, hoàn lại {tongSoLuongHoanLai} sản phẩm"
            };
        }

        public async Task<object> CreatePhieuHuyAsync(PhieuHuyDto phieuHuyDto)
        {
            // Validation
            if (phieuHuyDto == null || phieuHuyDto.ChiTietPhieuHuys == null || !phieuHuyDto.ChiTietPhieuHuys.Any())
            {
                throw new ArgumentException("Invalid input data or empty ChiTietPhieuHuys");
            }

            // Tạo phiếu hủy
            var phieuHuy = new PhieuHuy
            {
                MaPH = GenerateMaPhieuHuy(),
                NgayHuy = DateTime.Now,
                MaNV = phieuHuyDto.MaNV,
                LoaiHuy = phieuHuyDto.LoaiHuy?.ToUpper() == "HOADON" ? true : (phieuHuyDto.LoaiHuy?.ToUpper() == "KHO" ? false : (bool?)null),
                MaHD = phieuHuyDto.MaHD,
                TongSoLuongHuy = phieuHuyDto.ChiTietPhieuHuys.Sum(c => (int)c.SoLuongHuy)
            };

            // Tạo chi tiết phiếu hủy
            var chiTietPhieuHuyList = new List<ChiTietPhieuHuy>();
            foreach (var chiTiet in phieuHuyDto.ChiTietPhieuHuys)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(chiTiet.MaLo);
                if (tonKho == null)
                {
                    throw new ArgumentException($"Lô thuốc {chiTiet.MaLo} không tồn tại trong kho");
                }

                // Determine per-detail LoaiHuy: true => return to stock (add), false/null => normal (subtract/destroy)
                var detailLoaiHuy = chiTiet.LoaiHuy ?? false;

                    // If the whole phieu is from HOADON (phieuHuy.LoaiHuy == true), do not subtract stock for normal cancels
                    var phieuIsFromHoaDon = phieuHuy.LoaiHuy == true;
                    if (detailLoaiHuy)
                    {
                        // Return to stock: increase available quantity
                        tonKho.SoLuongCon += (int)chiTiet.SoLuongHuy;
                    }
                    else
                    {
                        // Normal cancel: only reduce stock when the phieu is not from HoaDon
                        if (!phieuIsFromHoaDon)
                        {
                            if (tonKho.SoLuongCon < chiTiet.SoLuongHuy)
                            {
                                throw new ArgumentException($"Không đủ số lượng lô {chiTiet.MaLo} trong kho");
                            }

                            tonKho.SoLuongCon -= (int)chiTiet.SoLuongHuy;
                        }
                        // else: phieu from HoaDon -> do not subtract (already subtracted when selling)
                    }

                await _repo.UpdateTonKhoAsync(tonKho);

                chiTietPhieuHuyList.Add(new ChiTietPhieuHuy
                {
                    MaCTPH = GenerateMaChiTietPhieuHuy(),
                    MaPH = phieuHuy.MaPH,
                    MaLo = chiTiet.MaLo,
                    SoLuongHuy = (int)chiTiet.SoLuongHuy,
                        DonGia = 0m, // Removed DonGia usage
                        ThanhTien = 0m, // Removed ThanhTien calculation
                    LyDoHuy = chiTiet.LyDoHuy,
                    GhiChu = chiTiet.GhiChu,
                    LoaiHuy = detailLoaiHuy
                });
            }

            // Lưu phiếu hủy và chi tiết
            await _repo.AddAsync(phieuHuy);
            await _repo.AddChiTietRangeAsync(chiTietPhieuHuyList);

            return new
            {
                MaPhieuHuy = phieuHuy.MaPH,
                NgayHuy = phieuHuy.NgayHuy,
                TongSoLuongHuy = phieuHuy.TongSoLuongHuy,
                Message = "Tạo phiếu hủy thành công"
            };
        }

        public async Task<object> UpdatePhieuHuyAsync(string maPH, PhieuHuyDto phieuHuyDto)
        {
            // Load existing PhieuHuy
            var existing = await _repo.GetByIdAsync(maPH);
            if (existing == null) throw new ArgumentException($"PhieuHuy {maPH} not found");

            // Revert stock effects from existing details
            var existingDetails = existing.ChiTietPhieuHuys.ToList();
            var existingPhieuIsFromHoaDon = existing.LoaiHuy == true;
            foreach (var old in existingDetails)
            {
                var ton = await _repo.GetTonKhoByMaLoAsync(old.MaLo);
                if (ton == null) continue; // nothing we can do

                // Revert logic depends on whether the original phieu was HOADON
                if (existingPhieuIsFromHoaDon)
                {
                    // For HOADON phieu, we only changed stock for details where LoaiHuy == true (we returned to stock)
                    // So to revert we must subtract the previously added quantity; if LoaiHuy == false there was no subtraction earlier.
                    if (old.LoaiHuy)
                    {
                        ton.SoLuongCon -= old.SoLuongHuy;
                        await _repo.UpdateTonKhoAsync(ton);
                    }
                }
                else
                {
                    // For KHO phieu, we applied either addition (old.LoaiHuy==true) or subtraction (old.LoaiHuy==false)
                    if (old.LoaiHuy)
                    {
                        // was added, so subtract to revert
                        ton.SoLuongCon -= old.SoLuongHuy;
                    }
                    else
                    {
                        // was subtracted, so add back
                        ton.SoLuongCon += old.SoLuongHuy;
                    }
                    await _repo.UpdateTonKhoAsync(ton);
                }
            }

            // Remove old details
            await _repo.DeleteChiTietPhieuHuyByMaPHAsync(maPH);

            // Update PhieuHuy header fields
            existing.MaNV = phieuHuyDto.MaNV;
            // convert LoaiHuy string -> bool? same as Create
            existing.LoaiHuy = phieuHuyDto.LoaiHuy?.ToUpper() == "HOADON" ? true : (phieuHuyDto.LoaiHuy?.ToUpper() == "KHO" ? false : (bool?)null);
            existing.MaHD = phieuHuyDto.MaHD;
            existing.GhiChu = phieuHuyDto.GhiChu;

            // Create new details and apply stock impact
            var newDetails = new List<ChiTietPhieuHuy>();
            foreach (var dto in phieuHuyDto.ChiTietPhieuHuys)
            {
                var tonKho = await _repo.GetTonKhoByMaLoAsync(dto.MaLo);
                if (tonKho == null) throw new ArgumentException($"TonKho {dto.MaLo} not found");

                var detailLoaiHuy = dto.LoaiHuy ?? false;
                var newPhieuIsFromHoaDon = phieuHuyDto.LoaiHuy?.ToUpper() == "HOADON";

                if (detailLoaiHuy)
                {
                    // return to stock
                    tonKho.SoLuongCon += (int)dto.SoLuongHuy;
                    await _repo.UpdateTonKhoAsync(tonKho);
                }
                else
                {
                    // only subtract when the new phieu is not from HoaDon
                    if (!newPhieuIsFromHoaDon)
                    {
                        if (tonKho.SoLuongCon < dto.SoLuongHuy)
                            throw new ArgumentException($"Not enough stock in {dto.MaLo} to apply update");

                        tonKho.SoLuongCon -= (int)dto.SoLuongHuy;
                        await _repo.UpdateTonKhoAsync(tonKho);
                    }
                    // else: new phieu is from HoaDon -> do not subtract
                }

                newDetails.Add(new ChiTietPhieuHuy
                {
                    MaCTPH = GenerateMaChiTietPhieuHuy(),
                    MaPH = existing.MaPH,
                    MaLo = dto.MaLo,
                    SoLuongHuy = (int)dto.SoLuongHuy,
                    DonGia = dto.DonGia ?? 0m,
                    ThanhTien = (dto.DonGia ?? 0m) * (int)dto.SoLuongHuy,
                    LyDoHuy = dto.LyDoHuy,
                    GhiChu = dto.GhiChu,
                    LoaiHuy = detailLoaiHuy
                });
            }

            // Save header and details
            existing.TongMatHangHuy = newDetails.Count;
            existing.TongSoLuongHuy = newDetails.Sum(x => x.SoLuongHuy);
            existing.TongTienHuy = newDetails.Where(x => !x.LoaiHuy).Sum(x => x.ThanhTien);
            existing.TongTienKho = newDetails.Where(x => x.LoaiHuy).Sum(x => x.ThanhTien);
            existing.TongTien = (existing.TongTienHuy ?? 0m) + (existing.TongTienKho ?? 0m);

            await _repo.UpdatePhieuHuyAsync(existing);
            if (newDetails.Any()) await _repo.AddChiTietRangeAsync(newDetails);

            // If PhieuHuy linked to HoaDon, update ChiTietHoaDon.TrangThaiXuLy for matched MaLo
            if (!string.IsNullOrEmpty(existing.MaHD))
            {
                var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(existing.MaHD);
                if (chiTietHoaDon != null && chiTietHoaDon.Any())
                {
                    var processedMaLos = newDetails.Select(d => d.MaLo).Distinct().ToList();
                    var affected = chiTietHoaDon.Where(ct => !string.IsNullOrEmpty(ct.MaLo) && processedMaLos.Contains(ct.MaLo)).ToList();
                    foreach (var c in affected)
                    {
                        c.TrangThaiXuLy = true;
                    }
                    await _repo.UpdateChiTietHoaDonRangeAsync(affected);

                    // After updating detail flags, update HoaDon.TrangThaiGiaoHang
                    await UpdateHoaDonStatusAfterHuyAsync(existing.MaHD!);
                }
            }

            return new { Message = "Cập nhật phiếu hủy thành công", PhieuHuy = existing };
        }

        public async Task<object> DeletePhieuHuyAsync(string maPH)
        {
            var existing = await _repo.GetByIdAsync(maPH);
            if (existing == null) throw new ArgumentException($"PhieuHuy {maPH} not found");

            // Revert stock effects based on stored details and phieu type
            var existingDetails = existing.ChiTietPhieuHuys.ToList();
            foreach (var old in existingDetails)
            {
                var ton = await _repo.GetTonKhoByMaLoAsync(old.MaLo);
                if (ton == null) continue;

                if (old.LoaiHuy)
                {
                    // old creation added to stock, so delete should subtract
                    ton.SoLuongCon -= old.SoLuongHuy;
                }
                else
                {
                    // old.LoaiHuy == false: creation behavior depends on header.LoaiHuy
                    if (existing.LoaiHuy == false || existing.LoaiHuy == null)
                    {
                        // KHO: creation subtracted stock, so revert by adding back
                        ton.SoLuongCon += old.SoLuongHuy;
                    }
                    else
                    {
                        // HOADON: creation did not subtract, so nothing to revert
                    }
                }

                await _repo.UpdateTonKhoAsync(ton);
            }

            // If linked to HoaDon, unset TrangThaiXuLy for matching MaLo
            if (!string.IsNullOrEmpty(existing.MaHD))
            {
                var chiTietHoaDon = await _repo.GetChiTietHoaDonByMaHDAsync(existing.MaHD);
                if (chiTietHoaDon != null && chiTietHoaDon.Any())
                {
                    var processedMaLos = existingDetails.Select(d => d.MaLo).Distinct().ToList();
                    var affected = chiTietHoaDon.Where(ct => ct.MaLo != null && processedMaLos.Contains(ct.MaLo)).ToList();
                    if (affected.Any())
                    {
                        foreach (var c in affected) c.TrangThaiXuLy = false;
                        await _repo.UpdateChiTietHoaDonRangeAsync(affected);
                    }

                    // Update HoaDon.TrangThaiGiaoHang accordingly
                    await UpdateHoaDonStatusAfterHuyAsync(existing.MaHD);
                }
            }

            // Delete details and phieu
            await _repo.DeleteChiTietPhieuHuyByMaPHAsync(maPH);
            await _repo.DeletePhieuHuyAsync(maPH);

            return new { Message = "Đã xoá phiếu hủy và hoàn tất phục hồi tồn kho/hoá đơn" };
        }

        private string GenerateMaPhieuHuy()
        {
            return "PH" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private string GenerateMaChiTietPhieuHuy()
        {
            return "CTPH" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}