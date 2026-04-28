# AUCA Certificate Portal

A professional ASP.NET Core 8 MVC web application designed for the **Adventist University of Central Africa (AUCA)** to manage and issue "Certificates of Good Standing".

## 🚀 Purpose
This portal provides a streamlined, secure way for the university registrar to:
- Search student attendance records.
- Preview certificates with high-fidelity institutional styling.
- Generate and download official certificates in **DOCX** and **PDF** formats.
- Maintain a secure audit trail of all issued documents.
- Verify certificate validity via integrated **QR codes**.

## 🛠 Prerequisites
- **.NET 8.0 SDK** or later.
- **SQL Server** (LocalDB, Express, or full version).
- A browser for the web interface.

## ⚙️ Setup Instructions
1. **Database Setup**:
   Run the following scripts in your SQL Server instance:
   ```sql
   -- 1. Create the Students Table
   CREATE TABLE [dbo].[CertificateOfAttendance] (
       StudentID NVARCHAR(100) PRIMARY KEY,
       StudentName NVARCHAR(200),
       BornDate DATETIME,
       StudiedFrom NVARCHAR(100),
       StudiedTo NVARCHAR(100),
       Year NVARCHAR(50),
       Faculty NVARCHAR(100),
       Major NVARCHAR(100),
       AcademicYear NVARCHAR(100),
       ApprovedBy NVARCHAR(100),
       Comment NVARCHAR(MAX),
       Status NVARCHAR(50) -- e.g., 'Approved', 'Pending'
   );

   -- 2. Create the Audit Log Table
   CREATE TABLE [dbo].[AuditLog] (
       Id INT IDENTITY PRIMARY KEY,
       Action NVARCHAR(100),
       StudentID NVARCHAR(100),
       PerformedAt DATETIME2 DEFAULT GETUTCDATE(),
       IPAddress NVARCHAR(50),
       UserAgent NVARCHAR(500)
   );
   ```

2. **Configuration**:
   Update `appsettings.json` with your connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=CertificatePortalDB;Trusted_Connection=True;"
   }
   ```

3. **Run the Application**:
   ```bash
   cd CertificatePortal
   dotnet run
   ```
   Access the portal at `https://localhost:5001`.

## 📜 Certificate Fields Mapping
The portal maps database columns directly to the document layout:
| DB Column | Document Section | Notes |
|-----------|------------------|-------|
| `StudentName` | **Student Name** | Bold, 12pt |
| `BornDate` | **Born on...** | Formatted as MMMM dd, yyyy |
| `StudentID` | **ID No.** | Also encoded in the QR Code |
| `StudiedFrom/To`| **Period** | e.g., "From Jan 2026 to Date" |
| `Faculty/Major` | **Academic Info**| Justified paragraph block |
| `ApprovedBy` | **Signature Line**| Name printed under signature line |

## 🔒 Production Hardening
- **Secrets**: Use Environment Variables or Azure Key Vault for connection strings.
- **Security Headers**: Injected automatically via middleware (HSTS, CSP, X-Frame-Options).
- **Validation**: Strict input sanitization and Antiforgery tokens on all requests.
- **Health Checks**: Monitor system status via `/health`.

## 📂 Folder Structure
```text
CertificatePortal/
├── Controllers/       # MVC Controllers (Certificate, Home)
├── Services/          # ICertificateService (DOCX/PDF Generation)
├── Repositories/      # Data Access Layer (Dapper + SQL Factory)
├── Models/            # Domain Models & ViewModels
├── Helpers/           # Security Middleware & Input Sanitizers
├── Views/             # Razor Templates (Institutional Styling)
└── wwwroot/           # Static assets (CSS/JS/Fonts/Images)
```

## 📄 License
Internal use only for the Adventist University of Central Africa.
