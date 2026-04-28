using System.Threading.Tasks;
using CertificatePortal.Models;
using Dapper;

namespace CertificatePortal.Repositories
{
    public interface IAuditLogRepository
    {
        Task LogAsync(AuditEntry entry);
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly bool _useMockData;

        public AuditLogRepository(IDbConnectionFactory connectionFactory, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _connectionFactory = connectionFactory;
            _useMockData = configuration.GetValue<bool>("UseMockData");
        }

        public async Task LogAsync(AuditEntry entry)
        {
            if (_useMockData)
            {
                // Skip DB logging in demo mode to prevent crashes
                return;
            }

            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO [dbo].[AuditLog] (Action, StudentID, PerformedAt, IPAddress, UserAgent)
                VALUES (@Action, @StudentID, @PerformedAt, @IPAddress, @UserAgent)";
            
            await connection.ExecuteAsync(sql, entry);
        }
    }
}
