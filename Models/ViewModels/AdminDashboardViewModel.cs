namespace SolarConnect.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalClients { get; set; }
        public int TotalVendors { get; set; }
        public int PendingApprovals { get; set; }
        public int ActiveRequests { get; set; }
    }
}
