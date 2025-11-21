//--------------------------Start Of File--------------------------//
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class ManagerDashboardViewModel
    {
        public List<Claim> ClaimsForApproval { get; set; } = new List<Claim>();
        public int UrgentClaims { get; set; }
        
        // ✅ FIXED: Changed to decimal for Rand amount
        public decimal TotalThisWeek { get; set; }
        
        public int PendingApproval { get; set; }
        public int ApprovedToday { get; set; }

        // Keep these for backward compatibility
        public List<Claim> VerifiedClaims { get; set; } = new List<Claim>();
        public int TotalVerified { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
    }
}
//--------------------------End Of File--------------------------//