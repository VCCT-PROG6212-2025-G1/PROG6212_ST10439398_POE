//--------------------------Start Of File--------------------------//
using System.Collections.Generic;
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class ManagerDashboardViewModel
    {
        public int PendingApproval { get; set; }
        public int ApprovedToday { get; set; }
        public int UrgentClaims { get; set; }
        public decimal TotalThisWeek { get; set; }
        public List<Claim> ClaimsForApproval { get; set; }
    }
}
//--------------------------End Of File--------------------------//