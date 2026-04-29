using System;

namespace CertificatePortal.Models
{
    public class CertificateRecord
    {
        public required string StudentName { get; set; }
        public DateTime? BornDate { get; set; }
        public required string StudentID { get; set; }
        public required string StudiedFrom { get; set; }
        public required string StudiedTo { get; set; }
        public required string Year { get; set; }
        public required string Faculty { get; set; }
        public required string Major { get; set; }
        public required string AcademicYear { get; set; }
        public required string ApprovedBy { get; set; }
        public required string Comment { get; set; }
        public required string Status { get; set; }
    }
}
