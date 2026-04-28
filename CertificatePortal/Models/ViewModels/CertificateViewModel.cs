using System;

namespace CertificatePortal.Models.ViewModels
{
    public class CertificateViewModel
    {
        public CertificateRecord Record { get; set; }

        public string FormattedBirthDate => Record.BornDate?.ToString("MMMM dd, yyyy") ?? "N/A";
        
        public string IssuedDate => DateTime.Now.ToString("MMMM dd, yyyy");

        public string CityAndDate => $"Kigali, {IssuedDate}";

        public CertificateViewModel(CertificateRecord record)
        {
            Record = record;
        }
    }
}
