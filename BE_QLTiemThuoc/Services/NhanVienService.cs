using System.Collections.Generic;
using System.Threading.Tasks;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Repositories;

namespace BE_QLTiemThuoc.Services
{
    public class NhanVienService
    {
        private readonly NhanVienRepository _repo;

        public NhanVienService(NhanVienRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<NhanVien>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async Task<NhanVien?> GetByIdAsync(string id)
        {
            return await _repo.GetByIdAsync(id);
        }

        public async Task<NhanVien> CreateAsync(NhanVien nhanVien)
        {
            if (string.IsNullOrWhiteSpace(nhanVien.MaNV))
                throw new ArgumentException("Mã nhân viên không được để trống");

            if (await _repo.GetByIdAsync(nhanVien.MaNV) != null)
                throw new InvalidOperationException("Mã nhân viên đã tồn tại");

            if (string.IsNullOrWhiteSpace(nhanVien.HoTen))
                throw new ArgumentException("Họ tên không được để trống");

            await _repo.CreateAsync(nhanVien);
            return nhanVien;
        }

        public async Task<NhanVien> UpdateAsync(NhanVien nhanVien)
        {
            if (string.IsNullOrWhiteSpace(nhanVien.MaNV))
                throw new ArgumentException("Mã nhân viên không được để trống");

            var existing = await _repo.GetByIdAsync(nhanVien.MaNV);
            if (existing == null)
                throw new KeyNotFoundException($"Không tìm thấy nhân viên với mã: {nhanVien.MaNV}");

            await _repo.UpdateAsync(nhanVien);
            return nhanVien;
        }

        public async Task<bool> DeleteAsync(string maNV)
        {
            if (string.IsNullOrWhiteSpace(maNV))
                throw new ArgumentException("Mã nhân viên không được để trống");

            return await _repo.DeleteAsync(maNV);
        }
    }
}