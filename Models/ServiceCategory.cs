using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class ServiceCategory
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();
}
