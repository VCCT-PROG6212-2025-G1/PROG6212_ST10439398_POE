//--------------------------Start Of File--------------------------//
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;
using System.Text;

namespace CMCS.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateClaimsReport(string reportType, DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateInvoice(int lecturerId, DateTime startDate, DateTime endDate);
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

        public async Task<byte[]> GenerateClaimsReport(string reportType, DateTime? startDate, DateTime? endDate)
        {
            try
            {
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

                var claims = await query.OrderByDescending(c => c.SubmissionDate).ToListAsync();

                // Generate HTML report (can be converted to PDF with external library)
                var html = GenerateClaimsReportHtml(claims, reportType, startDate, endDate);

                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating claims report");
                throw;
            }
        }

        public async Task<byte[]> GenerateInvoice(int lecturerId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Claims.Where(c => c.CurrentStatus == ClaimStatus.Approved))
                    .ThenInclude(c => c.Module)
                    .FirstOrDefaultAsync(u => u.UserId == lecturerId);

                if (user == null)
                {
                    throw new Exception("User not found");
                }

                var claims = user.Claims
                    .Where(c => c.SubmissionDate >= startDate && c.SubmissionDate <= endDate)
                    .ToList();

                // Generate HTML invoice (can be converted to PDF with external library)
                var html = GenerateInvoiceHtml(user, claims, startDate, endDate);

                return Encoding.UTF8.GetBytes(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for lecturer {LecturerId}", lecturerId);
                throw;
            }
        }

        private string GenerateClaimsReportHtml(List<Claim> claims, string reportType, DateTime? startDate, DateTime? endDate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<title>Claims Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #4CAF50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".summary { margin-top: 20px; padding: 15px; background-color: #e7f3ff; border-radius: 5px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<h1>CMCS Claims Report</h1>");
            sb.AppendLine($"<p>Report Type: {reportType}</p>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:dd MMM yyyy HH:mm}</p>");

            if (startDate.HasValue || endDate.HasValue)
            {
                sb.AppendLine($"<p>Period: {startDate?.ToString("dd MMM yyyy") ?? "Start"} - {endDate?.ToString("dd MMM yyyy") ?? "End"}</p>");
            }

            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Claim ID</th><th>Lecturer</th><th>Module</th><th>Hours</th><th>Rate</th><th>Total</th><th>Period</th></tr>");

            foreach (var claim in claims)
            {
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{claim.ClaimId}</td>");
                sb.AppendLine($"<td>{claim.User.FirstName} {claim.User.LastName}</td>");
                sb.AppendLine($"<td>{claim.Module?.ModuleName ?? "N/A"}</td>");
                sb.AppendLine($"<td>{claim.HoursWorked:N1}</td>");
                sb.AppendLine($"<td>R {claim.HourlyRate:N2}</td>");
                sb.AppendLine($"<td>R {claim.TotalAmount:N2}</td>");
                sb.AppendLine($"<td>{claim.ClaimPeriod}</td>");
                sb.AppendLine($"</tr>");
            }

            sb.AppendLine("</table>");

            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"<h3>Summary</h3>");
            sb.AppendLine($"<p>Total Claims: {claims.Count}</p>");
            sb.AppendLine($"<p>Total Hours: {claims.Sum(c => c.HoursWorked):N1}</p>");
            sb.AppendLine($"<p>Total Amount: R {claims.Sum(c => c.TotalAmount):N2}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private string GenerateInvoiceHtml(User user, List<Claim> claims, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<title>Invoice</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine(".header { border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }");
            sb.AppendLine(".invoice-info { display: flex; justify-content: space-between; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #2196F3; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".total { font-size: 1.2em; font-weight: bold; text-align: right; margin-top: 20px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>PAYMENT INVOICE</h1>");
            sb.AppendLine($"<p>Invoice Number: INV-{DateTime.Now:yyyyMMdd}-{user.UserId}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='invoice-info'>");
            sb.AppendLine("<div>");
            sb.AppendLine($"<p><strong>Lecturer:</strong> {user.FirstName} {user.LastName}</p>");
            sb.AppendLine($"<p><strong>Email:</strong> {user.Email}</p>");
            sb.AppendLine($"<p><strong>Department:</strong> {user.Department ?? "N/A"}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div>");
            sb.AppendLine($"<p><strong>Invoice Date:</strong> {DateTime.Now:dd MMM yyyy}</p>");
            sb.AppendLine($"<p><strong>Period:</strong> {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Claim ID</th><th>Module</th><th>Hours</th><th>Rate</th><th>Amount</th></tr>");

            foreach (var claim in claims)
            {
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{claim.ClaimId}</td>");
                sb.AppendLine($"<td>{claim.Module?.ModuleName ?? "N/A"}</td>");
                sb.AppendLine($"<td>{claim.HoursWorked:N1}</td>");
                sb.AppendLine($"<td>R {claim.HourlyRate:N2}</td>");
                sb.AppendLine($"<td>R {claim.TotalAmount:N2}</td>");
                sb.AppendLine($"</tr>");
            }

            sb.AppendLine("</table>");

            sb.AppendLine($"<p class='total'>Total Hours: {claims.Sum(c => c.HoursWorked):N1}</p>");
            sb.AppendLine($"<p class='total'>TOTAL AMOUNT DUE: R {claims.Sum(c => c.TotalAmount):N2}</p>");

            sb.AppendLine("</body></html>");

            return sb.ToString();
        }
    }
}
//--------------------------End Of File--------------------------//