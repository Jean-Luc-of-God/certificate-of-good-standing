using System;
using System.Globalization;

namespace CertificatePortal.Models.ViewModels
{
    public class CertificateViewModel
    {
        public CertificateRecord Record { get; set; }

        public string FormattedBirthDate => Record.BornDate?.ToString("MMMM dd, yyyy") ?? "N/A";
        
        public string IssuedDate => DateTime.Now.ToString("MMMM d, yyyy");

        public string CityAndDate => $"Kigali, {IssuedDate}";

        public string FormattedStudentName => ToTitleCase(Record.StudentName) + ",";

        public string EmailAddresses => "registrar@auca.ac.rw || juvenal.nsengiyumva@auca.ac.rw";
        
        public string MobilePhoneLabel => "Mobile Phone : ";

        public CertificateViewModel(CertificateRecord record)
        {
            Record = record;
        }

        private string ToTitleCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
        }
    }
}
