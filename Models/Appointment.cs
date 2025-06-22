using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public int CustomerId { get; set; }

    public int PetId { get; set; }

    public int DoctorId { get; set; }

    public int ServiceId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string AppointmentTime { get; set; } = null!;

    public double? Weight { get; set; }

    public int? Age { get; set; }

    public bool? IsNewPet { get; set; }

    public int? Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual Pet Pet { get; set; } = null!;

    public virtual Service Service { get; set; } = null!;
}
