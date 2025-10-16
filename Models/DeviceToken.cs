using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThuYBinhDuongAPI.Models
{
    /// <summary>
    /// Model cho Device Token - Lưu trữ FCM tokens của thiết bị
    /// </summary>
    [Table("DeviceToken")]
    public class DeviceToken
    {
        /// <summary>
        /// ID của token
        /// </summary>
        [Key]
        [Column("token_id")]
        public int TokenId { get; set; }

        /// <summary>
        /// ID của user sở hữu thiết bị
        /// </summary>
        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// FCM Device Token từ Firebase
        /// </summary>
        [Required]
        [Column("device_token")]
        [StringLength(500)]
        public string DeviceTokenValue { get; set; } = null!;

        /// <summary>
        /// Nền tảng: 'ios' hoặc 'android'
        /// </summary>
        [Required]
        [Column("platform")]
        [StringLength(20)]
        public string Platform { get; set; } = null!;

        /// <summary>
        /// Trạng thái active của token
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Ngày tạo token
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Ngày cập nhật token
        /// </summary>
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property
        /// <summary>
        /// User sở hữu token này
        /// </summary>
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}


