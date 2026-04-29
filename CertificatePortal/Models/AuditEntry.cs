using System;

namespace CertificatePortal.Models
{
    public class AuditEntry
    {
        public required string Action { get; set; }
        public required string StudentID { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public required string IPAddress { get; set; }
        public required string UserAgent { get; set; }
    }
}
