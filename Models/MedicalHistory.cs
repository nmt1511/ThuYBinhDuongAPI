using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class MedicalHistory
{
    public int HistoryId { get; set; }

    public int PetId { get; set; }

    public DateTime? RecordDate { get; set; }

    public string? Description { get; set; }

    public string? Treatment { get; set; }

    public string? Notes { get; set; }

    public virtual Pet Pet { get; set; } = null!;
}
