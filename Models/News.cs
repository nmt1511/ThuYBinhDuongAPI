using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class News
{
    public int NewsId { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? ImageUrl { get; set; }

    public string? Tags { get; set; }
}
