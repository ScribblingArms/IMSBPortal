using Microsoft.Extensions.Localization;

namespace IMSB.Admin.Areas.Portal
{
    /// <summary>
    /// The English messages written by wwwroot/portal/js/site.js and stall-form.js. The portal layout hands them to the
    /// scripts as window.IMSB_I18N (English text → text in the page's language). A message added to a script goes here
    /// and in Resources/PortalResource.te.resx and .hi.resx.
    /// </summary>
    public static class ScriptStrings
    {
        public static readonly string[] All =
        {
            // site.js
            "Text size changed", "High contrast on", "High contrast off", "Play slide rotation", "Pause slide rotation",

            // stall-form.js
            "(optional)", "Error:", "Select an option", "Tick this box to continue", "Upload {0}", "Upload no more than {0} files",
            "{0} must be a JPG, PNG or PDF", "{0} must be a JPG or PNG", "{0} must be smaller than {1} MB", "Check {0}", "Enter {0}",
            "Enter a valid {0}", "Check the format of {0}", "Enter a whole number for {0}", "Enter at least {0} characters",
            "Check this value", "Yes", "No", "Change", "Not given",
            "We could not reach the server. Check your connection and try again.",
            "The documents are too large to upload. Use files of 2 MB or less.",
            "The photos are too large to upload. Use up to 5 photos of 2 MB or less.",
            "We could not save your details. Please try again.", "{0} saved",
            "We could not submit your application. Please try again in a few minutes."
        };

        /// <summary>English text → translation, for the current UI culture.</summary>
        public static Dictionary<string, string> For(IStringLocalizer localizer) =>
            All.Distinct().ToDictionary(s => s, s => localizer[s].Value);
    }
}
