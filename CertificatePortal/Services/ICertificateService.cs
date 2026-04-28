using System.Threading.Tasks;
using CertificatePortal.Models.ViewModels;

namespace CertificatePortal.Services
{
    public interface ICertificateService
    {
        Task<byte[]> GenerateDocxAsync(CertificateViewModel model, string host);
        Task<byte[]> GeneratePdfAsync(CertificateViewModel model, string host);
    }
}
