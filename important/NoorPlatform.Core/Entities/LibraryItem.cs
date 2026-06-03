using System;

namespace NoorPlatform.Core.Entities
{
    public class LibraryItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // مصحف، متن، تجويد، منهج
        public string PdfFilePath { get; set; } = string.Empty;
        
        public int UploadedByUserId { get; set; }
        public User? UploadedByUser { get; set; }

        public int? CircleId { get; set; }
        public Circle? Circle { get; set; }

        public int DownloadCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
