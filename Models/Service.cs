using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Service
{
    public int ServiceId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int? Duration { get; set; }

    public int? CategoryId { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ServiceCategory? Category { get; set; }
}
