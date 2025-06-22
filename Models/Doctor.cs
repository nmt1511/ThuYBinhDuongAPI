using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Doctor
{
    public int DoctorId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Specialization { get; set; }

    public int? ExperienceYears { get; set; }

    public string? Branch { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
