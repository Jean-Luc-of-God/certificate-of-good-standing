using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificatePortal.Models;

namespace CertificatePortal.Repositories
{
    public class MockCertificateRepository : ICertificateRepository
    {
        private readonly List<CertificateRecord> _records = new List<CertificateRecord>
        {
            new CertificateRecord
            {
                StudentName = "Tuyizere Dieudonne",
                BornDate = new DateTime(2006, 6, 12),
                StudentID = "2025SEN349",
                StudiedFrom = "January 2026",
                StudiedTo = "Date",
                Year = "One (Semester 1)",
                Faculty = "Information Technology",
                Major = "Software Engineering",
                AcademicYear = "2025-2026",
                ApprovedBy = "Eng. Nsengiyumva Juvenal",
                Comment = "Exceptional performance in algorithmic logic.",
                Status = "Approved"
            },
            new CertificateRecord
            {
                StudentName = "Manishimwe Kwizera Jean Luc",
                BornDate = new DateTime(2000, 1, 1),
                StudentID = "AUCA-2026-999",
                StudiedFrom = "September 2022",
                StudiedTo = "August 2026",
                Year = "Four (Semester 2)",
                Faculty = "Information Technology",
                Major = "Software Engineering",
                AcademicYear = "2025-2026",
                ApprovedBy = "Eng. Nsengiyumva Juvenal",
                Comment = "Lead developer of the Certificate Portal project.",
                Status = "Approved"
            }
        };

        public Task<CertificateRecord?> GetByStudentIdAsync(string studentId)
        {
            var record = _records.FirstOrDefault(r => r.StudentID.Equals(studentId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(record);
        }

        public Task<IEnumerable<CertificateRecord>> SearchAsync(string searchTerm)
        {
            var results = _records.Where(r => 
                r.StudentName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                r.StudentID.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(results);
        }
    }

    public class MockAuditLogRepository : IAuditLogRepository
    {
        public Task LogAsync(AuditEntry entry) => Task.CompletedTask;
    }
}
