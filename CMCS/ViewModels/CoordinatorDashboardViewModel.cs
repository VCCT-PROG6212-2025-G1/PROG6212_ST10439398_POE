//--------------------------Start Of File--------------------------//
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class CoordinatorDashboardViewModel
    {
        public List<Claim> PendingClaims { get; set; } = new List<Claim>();
        public int TotalPending { get; set; }
        public int TotalVerified { get; set; }
        public int TotalRejected { get; set; }
    }
}
//--------------------------End Of File--------------------------//