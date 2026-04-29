namespace CertificatePortal.Models
{
    public class StudentModel
    {
        public required string StudentID { get; set; }
        public required string StudentName { get; set; }
        public required string Faculty { get; set; }
        public required string Status { get; set; }
        public bool IsApproved => string.Equals(Status, "Approved", System.StringComparison.OrdinalIgnoreCase);
    }
}
