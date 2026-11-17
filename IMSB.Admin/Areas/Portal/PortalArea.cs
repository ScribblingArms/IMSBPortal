namespace IMSB.Admin.Areas.Portal
{
    /// <summary>
    /// The public website (landing page and stall application), moved here from the former IMSB web project.
    /// It lives in the "Portal" MVC area (/Portal/...) so its HomeController, layout and views stay apart from the
    /// admin pages; its static files are under wwwroot/portal and its translations in Resources/PortalResource.*.resx.
    /// </summary>
    public static class PortalArea
    {
        public const string Name = "Portal";

        /// <summary>URL prefix of the portal's pages, e.g. for picking the portal's error page.</summary>
        public const string PathPrefix = "/" + Name;
    }
}
