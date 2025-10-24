using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class MedicalHistory
{
    public int HistoryId { get; set; }

    public int PetId { get; set; }

    public int? DoctorId { get; set; }

    public int? AppointmentId { get; set; }

    public DateTime? RecordDate { get; set; }

    public string? Description { get; set; }

    public string? Treatment { get; set; }

    public string? Notes { get; set; }

    public DateTime? NextAppointmentDate { get; set; }

    public int? NextServiceId { get; set; }

    public string? ReminderNote { get; set; }

    public bool? ReminderSent { get; set; }

    public virtual Pet Pet { get; set; } = null!;

    public virtual Doctor? Doctor { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual Service? NextService { get; set; }
}
