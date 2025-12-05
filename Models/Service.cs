using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Service
{
    public int ServiceId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public int? Duration { get; set; }

    public string? Category { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>
    /// Số ngày chu kỳ khuyến nghị sử dụng lại dịch vụ
    /// VD: 60 ngày (da liễu), 90 ngày (spa/grooming), 180 ngày (nha khoa), 365 ngày (tiêm phòng)
    /// </summary>
    public int? RecurrenceDays { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
