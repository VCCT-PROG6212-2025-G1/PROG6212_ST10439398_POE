//--------------------------Start Of File--------------------------//
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class CoordinatorDashboardViewModel
    {
        public List<Claim> ClaimsForReview { get; set; } = new List<Claim>();
        public int UrgentClaims { get; set; }
        
        // ? FIXED: Changed to decimal for Rand amount
        public decimal TotalThisWeek { get; set; }
        
        public int PendingReview { get; set; }
        public int ApprovedToday { get; set; }

        // Keep these for backward compatibility
        public List<Claim> PendingClaims { get; set; } = new List<Claim>();
        public int TotalPending { get; set; }
        public int TotalVerified { get; set; }
        public int TotalRejected { get; set; }
    }
}
//--------------------------End Of File--------------------------//