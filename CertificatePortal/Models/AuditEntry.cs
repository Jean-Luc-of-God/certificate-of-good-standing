using System;

namespace CertificatePortal.Models
{
    public class AuditEntry
    {
        public string Action { get; set; }
        public string StudentID { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
