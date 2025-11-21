//--------------------------Start Of File--------------------------//
using Microsoft.EntityFrameworkCore;
using CMCS.Data;
using CMCS.Models;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

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

                // Generate PDF report
                return GenerateClaimsReportPdf(claims, reportType, startDate, endDate);
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

                // Generate PDF invoice
                return GenerateInvoicePdf(user, claims, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for lecturer {LecturerId}", lecturerId);
                throw;
            }
        }

        private byte[] GenerateClaimsReportPdf(List<Claim> claims, string reportType, DateTime? startDate, DateTime? endDate)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Initialize PDF writer
                PdfWriter writer = new PdfWriter(memoryStream);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf);

                // Add title
                var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                Paragraph title = new Paragraph("CMCS Claims Report")
                    .SetFont(titleFont)
                    .SetFontSize(20)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER);
                document.Add(title);

                // Add report info
                document.Add(new Paragraph($"Report Type: {reportType}").SetFontSize(10));
                document.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}").SetFontSize(10));
                
                if (startDate.HasValue || endDate.HasValue)
                {
                    document.Add(new Paragraph($"Period: {startDate?.ToString("dd MMM yyyy") ?? "Start"} - {endDate?.ToString("dd MMM yyyy") ?? "End"}").SetFontSize(10));
                }

                document.Add(new Paragraph(" ")); // Spacer

                // Create table
                Table table = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2, 2, 1.5f, 1.5f, 1.5f, 1.5f }));
                table.SetWidth(UnitValue.CreatePercentValue(100));

                // Add header cells
                var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                table.AddHeaderCell(new Cell().Add(new Paragraph("Claim ID").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Lecturer").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Module").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Hours").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Rate").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Total").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                table.AddHeaderCell(new Cell().Add(new Paragraph("Period").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                // Add data rows
                foreach (var claim in claims)
                {
                    table.AddCell(new Cell().Add(new Paragraph(claim.ClaimId.ToString())));
                    table.AddCell(new Cell().Add(new Paragraph($"{claim.User.FirstName} {claim.User.LastName}")));
                    table.AddCell(new Cell().Add(new Paragraph(claim.Module?.ModuleName ?? "N/A")));
                    table.AddCell(new Cell().Add(new Paragraph(claim.HoursWorked.ToString("N1"))));
                    table.AddCell(new Cell().Add(new Paragraph($"R {claim.HourlyRate:N2}")));
                    table.AddCell(new Cell().Add(new Paragraph($"R {claim.TotalAmount:N2}")));
                    table.AddCell(new Cell().Add(new Paragraph(claim.ClaimPeriod)));
                }

                document.Add(table);

                // Add summary
                document.Add(new Paragraph(" ")); // Spacer
                Paragraph summary = new Paragraph("Summary")
                    .SetFont(titleFont)
                    .SetFontSize(14)
                    .SetBold();
                document.Add(summary);

                document.Add(new Paragraph($"Total Claims: {claims.Count}"));
                document.Add(new Paragraph($"Total Hours: {claims.Sum(c => c.HoursWorked):N1}"));
                document.Add(new Paragraph($"Total Amount: R {claims.Sum(c => c.TotalAmount):N2}").SetBold().SetFontSize(12));

                // Close document
                document.Close();

                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateInvoicePdf(User user, List<Claim> claims, DateTime startDate, DateTime endDate)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Initialize PDF writer
                PdfWriter writer = new PdfWriter(memoryStream);
                PdfDocument pdf = new PdfDocument(writer);
                Document document = new Document(pdf);

                // Add title
                var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                Paragraph title = new Paragraph("PAYMENT INVOICE")
                    .SetFont(titleFont)
                    .SetFontSize(22)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.CENTER);
                document.Add(title);

                document.Add(new Paragraph($"Invoice Number: INV-{DateTime.Now:yyyyMMdd}-{user.UserId}")
                    .SetTextAlignment(TextAlignment.CENTER));
                
                document.Add(new Paragraph(" ")); // Spacer

                // Lecturer information
                Table infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }));
                infoTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Left column
                Cell leftCell = new Cell().Add(new Paragraph($"Lecturer: {user.FirstName} {user.LastName}").SetBold());
                leftCell.Add(new Paragraph($"Email: {user.Email}"));
                leftCell.Add(new Paragraph($"Department: {user.Department ?? "N/A"}"));
                leftCell.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                // Right column
                Cell rightCell = new Cell().Add(new Paragraph($"Invoice Date: {DateTime.Now:dd MMM yyyy}").SetBold());
                rightCell.Add(new Paragraph($"Period: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}"));
                rightCell.SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                rightCell.SetTextAlignment(TextAlignment.RIGHT);

                infoTable.AddCell(leftCell);
                infoTable.AddCell(rightCell);
                document.Add(infoTable);

                document.Add(new Paragraph(" ")); // Spacer

                // Claims table
                Table claimsTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3, 1.5f, 1.5f, 2 }));
                claimsTable.SetWidth(UnitValue.CreatePercentValue(100));

                // Add header
                var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                claimsTable.AddHeaderCell(new Cell().Add(new Paragraph("Claim ID").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                claimsTable.AddHeaderCell(new Cell().Add(new Paragraph("Module").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                claimsTable.AddHeaderCell(new Cell().Add(new Paragraph("Hours").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                claimsTable.AddHeaderCell(new Cell().Add(new Paragraph("Rate").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
                claimsTable.AddHeaderCell(new Cell().Add(new Paragraph("Amount").SetFont(headerFont)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                // Add claim rows
                foreach (var claim in claims)
                {
                    claimsTable.AddCell(new Cell().Add(new Paragraph(claim.ClaimId.ToString())));
                    claimsTable.AddCell(new Cell().Add(new Paragraph(claim.Module?.ModuleName ?? "N/A")));
                    claimsTable.AddCell(new Cell().Add(new Paragraph(claim.HoursWorked.ToString("N1"))));
                    claimsTable.AddCell(new Cell().Add(new Paragraph($"R {claim.HourlyRate:N2}")));
                    claimsTable.AddCell(new Cell().Add(new Paragraph($"R {claim.TotalAmount:N2}")));
                }

                document.Add(claimsTable);

                document.Add(new Paragraph(" ")); // Spacer

                // Total section
                Paragraph totalHours = new Paragraph($"Total Hours: {claims.Sum(c => c.HoursWorked):N1}")
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetFontSize(12);
                document.Add(totalHours);

                Paragraph totalAmount = new Paragraph($"TOTAL AMOUNT DUE: R {claims.Sum(c => c.TotalAmount):N2}")
                    .SetFont(titleFont)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetFontSize(14)
                    .SetBold();
                document.Add(totalAmount);

                // Close document
                document.Close();

                return memoryStream.ToArray();
            }
        }
    }
}
//--------------------------End Of File--------------------------//