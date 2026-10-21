namespace IMSB.Interfaces
{
    public interface INotificationsInterface
    {
        /// <summary>
        /// Alerts worked out from the current data for the modules the user can open, most urgent first.
        /// Agreements and contracts ending within expiryWindowDays count as expiring.
        /// </summary>
        Task<List<Notification>> BuildAsync(bool isAdmin, bool isInventory, bool isStaff, int expiryWindowDays);
    }
}
