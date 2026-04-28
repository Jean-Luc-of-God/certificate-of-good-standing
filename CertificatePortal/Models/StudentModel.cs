namespace CertificatePortal.Models
{
    public class StudentModel
    {
        public string StudentID { get; set; }
        public string StudentName { get; set; }
        public string Faculty { get; set; }
        public string Status { get; set; }
        public bool IsApproved => string.Equals(Status, "Approved", System.StringComparison.OrdinalIgnoreCase);
    }
}
