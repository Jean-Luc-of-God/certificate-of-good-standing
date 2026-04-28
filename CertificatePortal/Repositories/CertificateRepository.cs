using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificatePortal.Models;
using Dapper;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace CertificatePortal.Repositories
{
    public interface ICertificateRepository
    {
        Task<CertificateRecord?> GetByStudentIdAsync(string studentId);
        Task<IEnumerable<CertificateRecord>> SearchAsync(string searchTerm);
    }

    public class CertificateRepository : ICertificateRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly bool _useMockData;
        private static readonly List<CertificateRecord> _mockRecords = new List<CertificateRecord>
        {
            new CertificateRecord { 
                StudentName = "MANISHIMWE Jean Luc", StudentID = "2024SEN112", BornDate = new System.DateTime(2002, 5, 20),
                Faculty = "Information Technology", Major = "Software Engineering", AcademicYear = "2024-2025",
                StudiedFrom = "Sept 2024", StudiedTo = "Date", Year = "1", ApprovedBy = "Eng. Nsengiyumva Juvenal", 
                Status = "Approved", Comment = "Consistently high grades."
            },
            new CertificateRecord { 
                StudentName = "MUKAMANZI Alice", StudentID = "2024BA045", BornDate = new System.DateTime(2003, 8, 12),
                Faculty = "Business Administration", Major = "Accounting", AcademicYear = "2024-2025",
                StudiedFrom = "Sept 2024", StudiedTo = "Date", Year = "1", ApprovedBy = "Eng. Nsengiyumva Juvenal", 
                Status = "Approved", Comment = ""
            },
            new CertificateRecord { 
                StudentName = "NTAKIRUTIMANA Eric", StudentID = "2023ED789", BornDate = new System.DateTime(2001, 3, 15),
                Faculty = "Education", Major = "Mathematics", AcademicYear = "2024-2025",
                StudiedFrom = "Sept 2023", StudiedTo = "Date", Year = "2", ApprovedBy = "Eng. Nsengiyumva Juvenal", 
                Status = "Approved", Comment = "Active in student leadership."
            },
            new CertificateRecord { 
                StudentName = "UWASE Brenda", StudentID = "2024SEN999", BornDate = new System.DateTime(2004, 11, 30),
                Faculty = "Information Technology", Major = "Cyber Security", AcademicYear = "2024-2025",
                StudiedFrom = "Sept 2024", StudiedTo = "Date", Year = "1", ApprovedBy = "Eng. Nsengiyumva Juvenal", 
                Status = "Approved", Comment = ""
            },
            new CertificateRecord { 
                StudentName = "HABIMANA Olivier", StudentID = "2022BS210", BornDate = new System.DateTime(2000, 12, 25),
                Faculty = "Business Administration", Major = "Finance", AcademicYear = "2024-2025",
                StudiedFrom = "Sept 2022", StudiedTo = "Date", Year = "3", ApprovedBy = "Eng. Nsengiyumva Juvenal", 
                Status = "Approved", Comment = "Dean's list candidate."
            }
        };

        public CertificateRepository(IDbConnectionFactory connectionFactory, IConfiguration configuration)
        {
            _connectionFactory = connectionFactory;
            _useMockData = configuration.GetValue<bool>("UseMockData");
        }

        public async Task<CertificateRecord?> GetByStudentIdAsync(string studentId)
        {
            if (_useMockData)
            {
                return _mockRecords.FirstOrDefault(r => r.StudentID.Equals(studentId, System.StringComparison.OrdinalIgnoreCase));
            }

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                const string query = "SELECT * FROM [dbo].[CertificateOfAttendance] WHERE StudentID = @StudentID";
                return await connection.QueryFirstOrDefaultAsync<CertificateRecord>(query, new { StudentID = studentId });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while fetching certificate for StudentID: {StudentID}", studentId);
                throw;
            }
        }

        public async Task<IEnumerable<CertificateRecord>> SearchAsync(string searchTerm)
        {
            if (_useMockData)
            {
                return _mockRecords.Where(r => 
                    r.StudentName.Contains(searchTerm, System.StringComparison.OrdinalIgnoreCase) || 
                    r.StudentID.Contains(searchTerm, System.StringComparison.OrdinalIgnoreCase));
            }

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                const string query = @"
                    SELECT * FROM [dbo].[CertificateOfAttendance] 
                    WHERE StudentName LIKE @term 
                    OR StudentID LIKE @term";
                
                return await connection.QueryAsync<CertificateRecord>(query, new { term = $"%{searchTerm}%" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while searching for certificates with term: {SearchTerm}", searchTerm);
                throw;
            }
        }
    }
}
