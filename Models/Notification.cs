using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThuYBinhDuongAPI.Models
{
    /// <summary>
    /// Model cho Notification - Lưu trữ thông báo cho user
    /// </summary>
    [Table("Notification")]
    public class Notification
    {
        /// <summary>
        /// ID của notification
        /// </summary>
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        /// <summary>
        /// ID của user nhận thông báo
        /// </summary>
        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Tiêu đề thông báo
        /// </summary>
        [Required]
        [Column("title")]
        [StringLength(200)]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Nội dung thông báo
        /// </summary>
        [Required]
        [Column("body")]
        [StringLength(500)]
        public string Body { get; set; } = null!;

        /// <summary>
        /// Loại thông báo: appointment_confirmed, appointment_status_change, etc.
        /// </summary>
        [Column("type")]
        [StringLength(50)]
        public string? Type { get; set; }

        /// <summary>
        /// Dữ liệu bổ sung (JSON)
        /// </summary>
        [Column("data")]
        public string? Data { get; set; }

        /// <summary>
        /// Đã đọc hay chưa
        /// </summary>
        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Ngày tạo
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Ngày đọc
        /// </summary>
        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        // Navigation property
        /// <summary>
        /// User nhận thông báo
        /// </summary>
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}

