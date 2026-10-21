using IMSB.Dto.Models;

namespace IMSB.Admin.Mappers
{
    /// <summary>A picker option: { id, name }.</summary>
    public record OptionItem(string Id, string Name);

    public static class LookupMapper
    {
        public static OptionItem ToOption(this ProductCategory c) => new(c.Id.ToString(), c.Name);

        public static IEnumerable<OptionItem> ToOptions(this IEnumerable<ProductCategory> categories) => categories.Select(ToOption);
    }
}
