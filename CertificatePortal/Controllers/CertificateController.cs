using System;
using System.Collections.Generic;
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
    [Route("Certificate/[action]")]
    [Route("")]
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
        [Route("~/")]
        [Route("/Certificate/Index")]
        public IActionResult Index()
        {
            return View(new CertificateSearchViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Search(CertificateSearchViewModel model)
        {
            // Sanitize search query
            model.Query = InputSanitizer.Sanitize(model.Query);

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

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
                _logger.LogError(ex, "Error searching for: {Query}", model.Query);
#if DEBUG
                ModelState.AddModelError("", $"Debug: {ex.Message} | {ex.InnerException?.Message}");
#else
                ModelState.AddModelError("", "An error occurred while searching. Please try again later.");
#endif
                return View("Index", model);
            }
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> Preview(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return BadRequest();
            }

            try
            {
                var record = await _repository.GetByStudentIdAsync(studentId);
                if (record == null)
                {
                    return NotFound();
                }

                var viewModel = new CertificateViewModel(record);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching preview for StudentID: {StudentID}", studentId);
                return View("Error");
            }
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> Download(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return BadRequest();
            }

            try
            {
                var record = await _repository.GetByStudentIdAsync(studentId);
                if (record == null)
                {
                    return NotFound();
                }

                // Audit Log
                await _auditRepository.LogAsync(new AuditEntry
                {
                    Action = "CERTIFICATE_DOWNLOADED",
                    StudentID = record.StudentID,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                var viewModel = new CertificateViewModel(record);
                var host = Request.Host.Value;
                var bytes = await _service.GenerateDocxAsync(viewModel, host);

                _logger.LogInformation("Certificate downloaded: StudentID {StudentID} at {Timestamp}", studentId, DateTime.Now);

                var fileName = $"Certificate_{record.StudentID}_{DateTime.Now:yyyyMMdd}.docx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading certificate for StudentID: {StudentID}", studentId);
                return View("Error");
            }
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> DownloadPdf(string studentId)
        {
            studentId = InputSanitizer.Sanitize(studentId);
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return BadRequest();
            }

            try
            {
                var record = await _repository.GetByStudentIdAsync(studentId);
                if (record == null)
                {
                    return NotFound();
                }

                await _auditRepository.LogAsync(new AuditEntry
                {
                    Action = "CERTIFICATE_PDF_DOWNLOADED",
                    StudentID = record.StudentID,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                var viewModel = new CertificateViewModel(record);
                var host = Request.Host.Value;
                var bytes = await _service.GeneratePdfAsync(viewModel, host);

                _logger.LogInformation("PDF Certificate downloaded: StudentID {StudentID} at {Timestamp}", studentId, DateTime.Now);

                var fileName = $"Certificate_{record.StudentID}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading PDF for StudentID: {StudentID}", studentId);
                return View("Error");
            }
        }
    }
}
