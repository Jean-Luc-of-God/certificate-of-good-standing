namespace CertificatePortal.Models
{
    public class AppSettings
    {
        public required string InstitutionName { get; set; }
        public required string RegistrarName { get; set; }
        public required string RegistrarTitle { get; set; }
        public required string ContactPhone { get; set; }
        public required string ContactEmail { get; set; }
        public required string LogoPath { get; set; }
    }
}
