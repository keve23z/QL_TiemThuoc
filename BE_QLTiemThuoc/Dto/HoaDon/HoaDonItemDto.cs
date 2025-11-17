using System;

namespace BE_QLTiemThuoc.Dto
{
    // Shared DTO for invoice item operations: create (online), update placeholders, confirm allocations.
    public class HoaDonItemDto
    {
        // (No MaCTHD here) Items are simple client requests; ConfirmOnline will find lots and create ChiTietHoaDon rows per-lot.

        // Medicine identifier
        public string? MaThuoc { get; set; }
        public string? DonVi { get; set; }

        // Quantity and pricing
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }

        // Optional dosage / lot-level fields
        public string? MaLD { get; set; }

        // Requested expiration date for allocation (used by Create/Confirm flows)
        public DateTime? HanSuDung { get; set; }
    }
}
