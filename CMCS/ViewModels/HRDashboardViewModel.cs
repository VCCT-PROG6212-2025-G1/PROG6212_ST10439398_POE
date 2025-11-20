//--------------------------Start Of File--------------------------//
using CMCS.Models;

namespace CMCS.ViewModels
{
    public class HRDashboardViewModel
    {
        public List<User> Users { get; set; } = new List<User>();
        public int TotalLecturers { get; set; }
        public int TotalCoordinators { get; set; }
        public int TotalManagers { get; set; }
        public int TotalUsers { get; set; }
        public int TotalApprovedClaims { get; set; }
        public decimal TotalPaymentAmount { get; set; }
    }
}
//--------------------------End Of File--------------------------//