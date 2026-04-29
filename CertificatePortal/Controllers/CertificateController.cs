using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CertificatePortal.Repositories;
using CertificatePortal.Services;
using CertificatePortal.Models.ViewModels;
using CertificatePortal.Models;
using CertificatePortal.Helpers;

namespace CertificatePortal.Controllers
{
    public class CertificateController : Controller
    {
        private readonly ICertificateRepository _repository;
        private readonly IAuditLogRepository _auditRepository;
        private readonly ICertificateService _service;
        private readonly ILogger<CertificateController> _logger;
        private readonly IConfiguration _configuration;

        public CertificateController(
            ICertificateRepository repository,
            IAuditLogRepository auditRepository,
            ICertificateService service,
            ILogger<CertificateController> logger,
            IConfiguration configuration)
        {
            _repository = repository;
            _auditRepository = auditRepository;
            _service = service;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("")]
        [Route("Certificate/Index")]
        public IActionResult Index()
        {
            return View(new CertificateSearchViewModel { Query = "" });
        }

        [HttpPost]
        [Route("Certificate/Search")]
        public async Task<IActionResult> Search(CertificateSearchViewModel model)
        {
            model.Query = InputSanitizer.Sanitize(model.Query);
            if (!ModelState.IsValid) return View("Index", model);

            try
            {
                var results = await _repository.SearchAsync(model.Query);
                model.Results = results;
                if (results == null || !results.Any())
                {
                    ModelState.AddModelError("Query", "No student found with that ID or name.");
                }
                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching");
                ModelState.AddModelError("", "An error occurred during search.");
                return View("Index", model);
            }
        }

        [HttpGet("Certificate/Preview/{studentId}")]
        public async Task<IActionResult> Preview(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            var record = await _repository.GetByStudentIdAsync(studentId);
            if (record == null) return NotFound();
            return View(new CertificateViewModel(record));
        }

        [HttpGet("Certificate/Download/{studentId}")]
        public async Task<IActionResult> Download(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            var record = await _repository.GetByStudentIdAsync(studentId);
            if (record == null) return NotFound();

            await _auditRepository.LogAsync(new AuditEntry 
            { 
                Action = "DOCX_DOWNLOAD", 
                StudentID = record.StudentID,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown"
            });
            
            var viewModel = new CertificateViewModel(record);
            var bytes = await _service.GenerateDocxAsync(viewModel, Request.Host.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Certificate_{record.StudentID}.docx");
        }

        [HttpGet("Certificate/DownloadPdf/{studentId}")]
        public async Task<IActionResult> DownloadPdf(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            var record = await _repository.GetByStudentIdAsync(studentId);
            if (record == null) return NotFound();

            await _auditRepository.LogAsync(new AuditEntry 
            { 
                Action = "PDF_DOWNLOAD", 
                StudentID = record.StudentID,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown"
            });
            
            var viewModel = new CertificateViewModel(record);
            var bytes = await _service.GeneratePdfAsync(viewModel, Request.Host.Value);
            return File(bytes, "application/pdf", $"Certificate_{record.StudentID}.pdf");
        }
    }
}
