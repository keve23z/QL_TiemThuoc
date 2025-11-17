using System;
using System.Collections.Generic;

namespace BE_QLTiemThuoc.Dto
{
    public class ConfirmOnlineDto
    {
            // Confirm an online invoice. Items are not required — details will be read from the DB.
            public string? MaHD { get; set; }
            public string? MaNV { get; set; }
    }
}
