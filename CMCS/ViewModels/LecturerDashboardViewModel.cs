// ViewModels/LecturerDashboardViewModel.cs
using System.Collections.Generic;
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class LecturerDashboardViewModel
    {
        public int PendingClaims { get; set; }
        public decimal MonthlyTotal { get; set; }
        public decimal YearToDate { get; set; }
        public int ClaimsThisMonth { get; set; }
        public List<Claim> RecentClaims { get; set; }
    }
}