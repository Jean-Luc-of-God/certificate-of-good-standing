using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CertificatePortal.Models.ViewModels
{
    public class CertificateSearchViewModel
    {
        [Required(ErrorMessage = "Please enter a Student ID or Name.")]
        public required string Query { get; set; }

        public IEnumerable<CertificateRecord> Results { get; set; } = new List<CertificateRecord>();
    }
}
