using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThuYBinhDuongAPI.Models
{
    [Table("ChatMessage")]
    public class ChatMessage
    {
        [Key]
        [Column("message_id")]
        public int MessageId { get; set; }

        [Required]
        [Column("room_id")]
        public int RoomId { get; set; }

        [Required]
        [Column("sender_id")]
        public int SenderId { get; set; }

        [Required]
        [Column("sender_type")]
        public int SenderType { get; set; } // 0: Customer, 1: Admin

        [Required]
        [Column("message_content")]
        public string MessageContent { get; set; } = string.Empty;

        [Required]
        [Column("message_type")]
        public int MessageType { get; set; } // 0: Text, 1: Image, 2: File

        [StringLength(500)]
        [Column("file_url")]
        public string? FileUrl { get; set; }

        [Required]
        [Column("is_read")]
        public bool IsRead { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("RoomId")]
        public virtual ChatRoom Room { get; set; } = null!;
    }
}

