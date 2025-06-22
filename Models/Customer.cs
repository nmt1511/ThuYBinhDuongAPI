using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Customer
{
    public int CustomerId { get; set; }

    public int UserId { get; set; }

    public string CustomerName { get; set; } = null!;

    public int? Gender { get; set; }

    public string? Address { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Pet> Pets { get; set; } = new List<Pet>();

    public virtual User User { get; set; } = null!;
}
