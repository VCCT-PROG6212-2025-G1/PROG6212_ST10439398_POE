//--------------------------Start Of File--------------------------//
using CMCS.Data;
using CMCS.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CMCS.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateApprovedClaimsReportAsync(DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateInvoiceAsync(int claimId);
        Task<byte[]> GenerateLecturerReportAsync(int lecturerId, DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateMonthlyReportAsync(int year, int month);
    }

    public class ReportService : IReportService
    {
        private readonly CMCSContext _context;
        private readonly ILogger<ReportService> _logger;

        public ReportService(CMCSContext context, ILogger<ReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Generate a report of all approved claims within a date range
        /// Uses LINQ for querying - as specified in requirements
        /// </summary>
        public async Task<byte[]> GenerateApprovedClaimsReportAsync(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Use LINQ to query approved claims
                var query = _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.CurrentStatus == ClaimStatus.Approved);

                if (startDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate <= endDate.Value);
                }

                var claims = await query
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                // Generate HTML report
                var html = GenerateClaimsReportHtml(claims, startDate, endDate);

                // Convert to PDF bytes (simplified - returns HTML as bytes)
                // In production, you would use a library like iTextSharp, DinkToPdf, or SelectPdf
                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating approved claims report");
                throw;
            }
        }

        /// <summary>
        /// Generate an invoice for a specific claim
        /// </summary>
        public async Task<byte[]> GenerateInvoiceAsync(int claimId)
        {
            try
            {
                var claim = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);

                if (claim == null)
                {
                    throw new Exception($"Claim {claimId} not found");
                }

                var html = GenerateInvoiceHtml(claim);
                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for claim {ClaimId}", claimId);
                throw;
            }
        }

        /// <summary>
        /// Generate a report for a specific lecturer
        /// </summary>
        public async Task<byte[]> GenerateLecturerReportAsync(int lecturerId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var query = _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.UserId == lecturerId);

                if (startDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(c => c.SubmissionDate <= endDate.Value);
                }

                var claims = await query.OrderByDescending(c => c.SubmissionDate).ToListAsync();
                var lecturer = await _context.Users.FindAsync(lecturerId);

                var html = GenerateLecturerReportHtml(lecturer, claims, startDate, endDate);
                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating lecturer report for {LecturerId}", lecturerId);
                throw;
            }
        }

        /// <summary>
        /// Generate a monthly summary report
        /// </summary>
        public async Task<byte[]> GenerateMonthlyReportAsync(int year, int month)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Include(c => c.Module)
                    .Where(c => c.SubmissionDate >= startDate && c.SubmissionDate <= endDate)
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                var html = GenerateMonthlyReportHtml(claims, year, month);
                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly report for {Year}-{Month}", year, month);
                throw;
            }
        }

        #region HTML Report Generators

        private string GenerateClaimsReportHtml(List<Claim> claims, DateTime? startDate, DateTime? endDate)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine("<title>Approved Claims Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #2563eb; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #2563eb; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine(".total { font-weight: bold; font-size: 1.2em; margin-top: 20px; }");
            sb.AppendLine(".header-info { margin-bottom: 20px; color: #666; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<h1>📋 Approved Claims Report</h1>");
            sb.AppendLine("<div class='header-info'>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            if (startDate.HasValue || endDate.HasValue)
            {
                sb.AppendLine($"<p>Period: {startDate?.ToString("yyyy-MM-dd") ?? "Start"} to {endDate?.ToString("yyyy-MM-dd") ?? "End"}</p>");
            }
            sb.AppendLine($"<p>Total Claims: {claims.Count}</p>");
            sb.AppendLine("</div>");

            if (claims.Any())
            {
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Claim ID</th><th>Lecturer</th><th>Module</th><th>Hours</th><th>Rate</th><th>Total</th><th>Date</th></tr>");

                foreach (var claim in claims)
                {
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"<td>CLC-{claim.ClaimId:D4}</td>");
                    sb.AppendLine($"<td>{claim.User?.FirstName} {claim.User?.LastName}</td>");
                    sb.AppendLine($"<td>{claim.Module?.ModuleCode}</td>");
                    sb.AppendLine($"<td>{claim.HoursWorked:N2}</td>");
                    sb.AppendLine($"<td>R{claim.HourlyRate:N2}</td>");
                    sb.AppendLine($"<td>R{claim.TotalAmount:N2}</td>");
                    sb.AppendLine($"<td>{claim.SubmissionDate:yyyy-MM-dd}</td>");
                    sb.AppendLine($"</tr>");
                }

                sb.AppendLine("</table>");
                sb.AppendLine($"<p class='total'>Total Amount: R{claims.Sum(c => c.TotalAmount):N2}</p>");
            }
            else
            {
                sb.AppendLine("<p>No approved claims found for the selected period.</p>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerateInvoiceHtml(Claim claim)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine("<title>Invoice</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine(".invoice-header { text-align: center; border-bottom: 2px solid #2563eb; padding-bottom: 20px; }");
            sb.AppendLine(".invoice-details { margin: 20px 0; }");
            sb.AppendLine(".invoice-table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            sb.AppendLine(".invoice-table th, .invoice-table td { border: 1px solid #ddd; padding: 12px; }");
            sb.AppendLine(".invoice-table th { background-color: #f5f5f5; }");
            sb.AppendLine(".total-row { font-weight: bold; background-color: #e8f4f8; }");
            sb.AppendLine(".footer { margin-top: 40px; text-align: center; color: #666; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<div class='invoice-header'>");
            sb.AppendLine("<h1>INVOICE</h1>");
            sb.AppendLine("<p>Contract Monthly Claim System</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='invoice-details'>");
            sb.AppendLine($"<p><strong>Invoice Number:</strong> INV-{claim.ClaimId:D6}</p>");
            sb.AppendLine($"<p><strong>Date:</strong> {DateTime.Now:yyyy-MM-dd}</p>");
            sb.AppendLine($"<p><strong>Claim ID:</strong> CLC-{claim.ClaimId:D4}</p>");
            sb.AppendLine($"<p><strong>Lecturer:</strong> {claim.User?.FirstName} {claim.User?.LastName}</p>");
            sb.AppendLine($"<p><strong>Email:</strong> {claim.User?.Email}</p>");
            sb.AppendLine($"<p><strong>Claim Period:</strong> {claim.ClaimPeriod}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<table class='invoice-table'>");
            sb.AppendLine("<tr><th>Description</th><th>Hours</th><th>Rate</th><th>Amount</th></tr>");
            sb.AppendLine($"<tr>");
            sb.AppendLine($"<td>{claim.Module?.ModuleName} ({claim.Module?.ModuleCode})</td>");
            sb.AppendLine($"<td>{claim.HoursWorked:N2}</td>");
            sb.AppendLine($"<td>R{claim.HourlyRate:N2}</td>");
            sb.AppendLine($"<td>R{claim.TotalAmount:N2}</td>");
            sb.AppendLine($"</tr>");
            sb.AppendLine($"<tr class='total-row'>");
            sb.AppendLine($"<td colspan='3'>Total</td>");
            sb.AppendLine($"<td>R{claim.TotalAmount:N2}</td>");
            sb.AppendLine($"</tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>Thank you for your service!</p>");
            sb.AppendLine("<p>Payment will be processed within 30 days.</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerateLecturerReportHtml(User lecturer, List<Claim> claims, DateTime? startDate, DateTime? endDate)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine($"<title>Lecturer Report - {lecturer?.FirstName} {lecturer?.LastName}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #2563eb; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #2563eb; color: white; }");
            sb.AppendLine(".summary { background-color: #f5f5f5; padding: 20px; margin: 20px 0; border-radius: 8px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine($"<h1>Lecturer Report: {lecturer?.FirstName} {lecturer?.LastName}</h1>");
            sb.AppendLine($"<p>Email: {lecturer?.Email}</p>");
            sb.AppendLine($"<p>Hourly Rate: R{lecturer?.HourlyRate:N2}</p>");

            var approved = claims.Where(c => c.CurrentStatus == ClaimStatus.Approved).ToList();
            var pending = claims.Where(c => c.CurrentStatus == ClaimStatus.Submitted || c.CurrentStatus == ClaimStatus.UnderReview).ToList();

            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"<p><strong>Total Claims:</strong> {claims.Count}</p>");
            sb.AppendLine($"<p><strong>Approved:</strong> {approved.Count} (R{approved.Sum(c => c.TotalAmount):N2})</p>");
            sb.AppendLine($"<p><strong>Pending:</strong> {pending.Count}</p>");
            sb.AppendLine($"<p><strong>Total Hours:</strong> {claims.Sum(c => c.HoursWorked):N2}</p>");
            sb.AppendLine("</div>");

            if (claims.Any())
            {
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Claim ID</th><th>Module</th><th>Hours</th><th>Total</th><th>Status</th><th>Date</th></tr>");
                foreach (var claim in claims)
                {
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"<td>CLC-{claim.ClaimId:D4}</td>");
                    sb.AppendLine($"<td>{claim.Module?.ModuleCode}</td>");
                    sb.AppendLine($"<td>{claim.HoursWorked:N2}</td>");
                    sb.AppendLine($"<td>R{claim.TotalAmount:N2}</td>");
                    sb.AppendLine($"<td>{claim.CurrentStatus}</td>");
                    sb.AppendLine($"<td>{claim.SubmissionDate:yyyy-MM-dd}</td>");
                    sb.AppendLine($"</tr>");
                }
                sb.AppendLine("</table>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerateMonthlyReportHtml(List<Claim> claims, int year, int month)
        {
            var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine($"<title>Monthly Report - {monthName}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #2563eb; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #2563eb; color: white; }");
            sb.AppendLine(".summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin: 20px 0; }");
            sb.AppendLine(".stat-card { background: #f5f5f5; padding: 20px; border-radius: 8px; text-align: center; }");
            sb.AppendLine(".stat-value { font-size: 2em; font-weight: bold; color: #2563eb; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine($"<h1>📊 Monthly Report: {monthName}</h1>");

            var approved = claims.Where(c => c.CurrentStatus == ClaimStatus.Approved).ToList();
            var totalAmount = approved.Sum(c => c.TotalAmount);

            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"<div class='stat-card'><div class='stat-value'>{claims.Count}</div><div>Total Claims</div></div>");
            sb.AppendLine($"<div class='stat-card'><div class='stat-value'>{approved.Count}</div><div>Approved</div></div>");
            sb.AppendLine($"<div class='stat-card'><div class='stat-value'>R{totalAmount:N0}</div><div>Total Value</div></div>");
            sb.AppendLine("</div>");

            if (claims.Any())
            {
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Claim ID</th><th>Lecturer</th><th>Module</th><th>Hours</th><th>Total</th><th>Status</th></tr>");
                foreach (var claim in claims)
                {
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"<td>CLC-{claim.ClaimId:D4}</td>");
                    sb.AppendLine($"<td>{claim.User?.FirstName} {claim.User?.LastName}</td>");
                    sb.AppendLine($"<td>{claim.Module?.ModuleCode}</td>");
                    sb.AppendLine($"<td>{claim.HoursWorked:N2}</td>");
                    sb.AppendLine($"<td>R{claim.TotalAmount:N2}</td>");
                    sb.AppendLine($"<td>{claim.CurrentStatus}</td>");
                    sb.AppendLine($"</tr>");
                }
                sb.AppendLine("</table>");
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        #endregion
    }
}
//--------------------------End Of File--------------------------//