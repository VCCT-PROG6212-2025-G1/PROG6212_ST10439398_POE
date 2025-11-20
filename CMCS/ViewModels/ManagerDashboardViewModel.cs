//--------------------------Start Of File--------------------------//
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class ManagerDashboardViewModel
    {
        public List<Claim> VerifiedClaims { get; set; } = new List<Claim>();
        public int TotalVerified { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
    }
}
//--------------------------End Of File--------------------------//