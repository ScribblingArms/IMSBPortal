using IMSB.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Alerts worked out from the current data for the modules the user's role can open: open applications, pending
    /// reallocations, agreements and contracts running out, renewals and low stock. The app builds the text from type + count.
    /// </summary>
    public class NotificationsController : ApiControllerBase
    {
        public const int ExpiryWindowDays = 30;

        private readonly INotificationsInterface _notifications;

        public NotificationsController(INotificationsInterface notifications) => _notifications = notifications;

        [HttpGet]
        public async Task<IActionResult> Data()
        {
            var items = await _notifications.BuildAsync(User.IsInRole("Admin"), User.IsInRole("Inventory"), User.IsInRole("Staff"), ExpiryWindowDays);
            return Ok(new { items, total = items.Sum(n => n.Count) });
        }
    }
}
