using IMSB.Dto.Models;

namespace IMSB.Admin.Areas.Portal.Models
{
    /// <summary>Dropdown data for the stall application form, loaded from the database.</summary>
    public class StallApplyFormViewModel
    {
        public List<District> Districts { get; init; } = new();

        /// <summary>From product_categories, names trimmed.</summary>
        public List<ProductCategory> Categories { get; init; } = new();
    }
}
