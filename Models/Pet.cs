﻿using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Pet
{
    public int PetId { get; set; }

    public int CustomerId { get; set; }

    public string Name { get; set; } = null!;

    public string? Species { get; set; }

    public string? Breed { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? ImageUrl { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<MedicalHistory> MedicalHistories { get; set; } = new List<MedicalHistory>();
}
