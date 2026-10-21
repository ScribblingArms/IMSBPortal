using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// The header bell. Alerts are worked out from the current data (nothing is stored): open applications, pending
    /// stall reallocations, agreements and staff contracts running out, renewals and low stock. Each user only gets
    /// alerts for the modules their role can open. The page builds the (localized) text from type + count.
    /// </summary>
    public class NotificationsController : Controller
    {
        public const int ExpiryWindowDays = 30;

        private readonly INotificationsInterface _notifications;

        public NotificationsController(INotificationsInterface notifications) => _notifications = notifications;

        [HttpGet]
        public async Task<IActionResult> Data()
        {
            var list = await BuildAsync();
            return Ok(new { items = list, total = list.Sum(n => n.Count) });
        }

        internal Task<List<Notification>> BuildAsync() =>
            _notifications.BuildAsync(User.IsInRole("Admin"), User.IsInRole("Inventory"), User.IsInRole("Staff"), ExpiryWindowDays);
    }
}
