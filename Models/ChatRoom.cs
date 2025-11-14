using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThuYBinhDuongAPI.Models
{
    [Table("ChatRoom")]
    public class ChatRoom
    {
        [Key]
        [Column("room_id")]
        public int RoomId { get; set; }

        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("admin_user_id")]
        public int? AdminUserId { get; set; }

        [StringLength(255)]
        [Column("room_name")]
        public string? RoomName { get; set; }

        [Required]
        [Column("status")]
        public int Status { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("last_message_at")]
        public DateTime? LastMessageAt { get; set; }

        [StringLength(500)]
        [Column("last_message")]
        public string? LastMessage { get; set; }

        [Required]
        [Column("unread_count_customer")]
        public int UnreadCountCustomer { get; set; }

        [Required]
        [Column("unread_count_admin")]
        public int UnreadCountAdmin { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("AdminUserId")]
        public virtual User? AdminUser { get; set; }

        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}

