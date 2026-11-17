namespace IMSB.Admin
{
    /// <summary>
    /// Marker for the public website's translations (Resources/PortalResource.te.resx / .hi.resx). Keys are the English
    /// text itself, so English needs no resource file: a key with no translation is shown as written.
    /// Kept in the root namespace (and outside the Resources folder) so the localizer's resource name is
    /// IMSB.Admin.Resources.PortalResource, matching the embedded .resx files. The admin pages use SharedResource instead.
    /// </summary>
    public class PortalResource
    {
    }
}
