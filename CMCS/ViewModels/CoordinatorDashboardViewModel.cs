// ViewModels/CoordinatorDashboardViewModel.cs
using System.Collections.Generic;
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class CoordinatorDashboardViewModel
    {
        public int PendingReview { get; set; }
        public int ApprovedToday { get; set; }
        public int UrgentClaims { get; set; }
        public decimal TotalThisWeek { get; set; }
        public List<Claim> ClaimsForReview { get; set; }
    }
}