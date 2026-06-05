using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoorPlatform.Core.Entities
{
    public enum AuditAction
    {
        Create,
        Update,
        Delete
    }

    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public AuditAction Action { get; set; }

        [Required]
        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string EntityId { get; set; } = string.Empty;

        public string? OldValues { get; set; } // Stored as JSON string
        public string? NewValues { get; set; } // Stored as JSON string

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
