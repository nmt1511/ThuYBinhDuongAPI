﻿using System;
using System.Collections.Generic;

namespace ThuYBinhDuongAPI.Models;

public partial class Feedback
{
    public int FeedbackId { get; set; }

    public int AppointmentId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;
}
