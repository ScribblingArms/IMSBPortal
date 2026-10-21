namespace IMSB.Dto.Models
{
    // Development-only sample data (see Program.cs): the 120-stall layout from the prototype (blocks A-F, 20 each)
    // and a few artisans holding the stalls that the demo inventory already uses. Each part is seeded only when its table is empty.
    public static class DemoBazaar
    {
        private record SampleArtisan(string Name, string Mobile, string District, string Category, int Years, string? Stall, string Status);

        private static readonly SampleArtisan[] Artisans =
        {
            new("Kamala Rajendran", "9876543210", "Hyderabad", "Textiles", 12, "A-01", ArtisanStatuses.Active),
            new("Saroja Murugan", "8765432109", "Warangal", "Handicrafts", 8, "B-04", ArtisanStatuses.Active),
            new("Priya Kumar", "7654321098", "Karimnagar", "Jewellery", 6, "E-06", ArtisanStatuses.Active),
            new("Meena Selvam", "6543210987", "Nalgonda", "Pottery", 15, "C-03", ArtisanStatuses.OnLeave),
            new("Radha Krishnan", "9543210876", "Khammam", "Paintings", 10, "D-09", ArtisanStatuses.Active),
            new("Lakshmi Velu", "9432109876", "Nizamabad", "Herbal", 4, "F-02", ArtisanStatuses.RenewalDue),
            new("Parvathi Thangam", "9321098765", "Medak", "Food & Snacks", 9, "F-05", ArtisanStatuses.Active),
            new("Anitha Jayakumar", "9210987654", "Siddipet", "Textiles", 3, null, ArtisanStatuses.Active),
        };

        public static void SeedIfEmpty(IMSBContext context)
        {
            if (!context.Stalls.Any())
            {
                foreach (var block in new[] { "A", "B", "C", "D", "E", "F" })
                    for (int n = 1; n <= 20; n++)
                        context.Stalls.Add(new Stall
                        {
                            StallCode = $"{block}-{n:D2}",
                            Name = $"Stall {block}-{n:D2}",
                            Block = block,
                            RowLabel = $"Row {(n - 1) / 5 + 1}",
                            Status = StallStatuses.Vacant,
                            CreatedBy = "seed"
                        });
                context.SaveChanges();
            }

            var categories = context.ProductCategories.ToDictionary(c => c.Name, c => c.Id);
            SeedArtisans(context, categories);
            SeedApplications(context, categories);
        }

        private static void SeedArtisans(IMSBContext context, Dictionary<string, Guid> categories)
        {
            if (context.Artisans.Any()) return;
            var i = 0;
            foreach (var s in Artisans)
            {
                if (!categories.TryGetValue(s.Category, out var categoryId)) continue;
                i++;
                var eleven = $"5{i:D2}{s.Mobile[..8]}";
                var aadhaar = eleven + Aadhaar.CheckDigit(eleven);
                var artisan = new Artisan
                {
                    ArtisanCode = $"ART-{i:D4}",
                    FullName = s.Name,
                    DateOfBirth = new DateTime(1980 + i, i, 10 + i),
                    Gender = "Female",
                    Mobile = s.Mobile,
                    Address = $"{10 + i}, Main Road, {s.District}",
                    AadhaarHash = Aadhaar.Hash(aadhaar),
                    AadhaarLast4 = Aadhaar.Last4(aadhaar),
                    District = s.District,
                    CategoryId = categoryId,
                    YearsOfExperience = s.Years,
                    Status = s.Status,
                    JoinedDate = DateTime.Today.AddMonths(-3 * i),
                    CreatedBy = "seed"
                };
                context.Artisans.Add(artisan);
                var stall = s.Stall == null ? null : context.Stalls.FirstOrDefault(x => x.StallCode == s.Stall && x.ArtisanId == null);
                if (stall != null)
                {
                    stall.ArtisanId = artisan.Id;
                    stall.Status = StallStatuses.Occupied;
                    stall.CategoryId = categoryId;
                    stall.AllottedDate = artisan.JoinedDate;
                    stall.AgreementEndDate = artisan.JoinedDate.AddYears(2);
                    stall.MonthlyRent = 2500;
                }
            }
            context.SaveChanges();
        }

        private record SampleApplication(string Name, string Mobile, string District, string Category, int Years, int DaysAgo, string Status);

        private static readonly SampleApplication[] Applications =
        {
            new("Sridevi Mallesh", "9012345601", "Warangal", "Handicrafts", 5, 2, ApplicationStatuses.Pending),
            new("Geetha Bhaskar", "9012345602", "Karimnagar", "Pottery", 11, 6, ApplicationStatuses.Pending),
            new("Vani Prasad", "9012345603", "Hyderabad", "Jewellery", 2, 9, ApplicationStatuses.Pending),
            new("Deepa Narsimha", "9012345604", "Nalgonda", "Textiles", 7, 12, ApplicationStatuses.UnderReview),
            new("Rani Yadav", "9012345605", "Khammam", "Food & Snacks", 4, 15, ApplicationStatuses.UnderReview),
        };

        /// <summary>A few open applications, only when that table is empty. Skips any whose mobile already belongs to an artisan.</summary>
        private static void SeedApplications(IMSBContext context, Dictionary<string, Guid> categories)
        {
            if (context.ArtisanApplications.Any()) return;
            var i = 0;
            foreach (var s in Applications)
            {
                if (!categories.TryGetValue(s.Category, out var categoryId) || context.Artisans.Any(a => a.Mobile == s.Mobile)) continue;
                i++;
                var eleven = $"6{i:D2}{s.Mobile[..8]}";
                var aadhaar = eleven + Aadhaar.CheckDigit(eleven);
                context.ArtisanApplications.Add(new ArtisanApplication
                {
                    ApplicationNo = $"APP-{i:D4}",
                    FullName = s.Name,
                    DateOfBirth = new DateTime(1985 + i, i, 5 + i),
                    Gender = "Female",
                    Mobile = s.Mobile,
                    Address = $"{20 + i}, Bazaar Street, {s.District}",
                    AadhaarHash = Aadhaar.Hash(aadhaar),
                    AadhaarLast4 = Aadhaar.Last4(aadhaar),
                    District = s.District,
                    CategoryId = categoryId,
                    YearsOfExperience = s.Years,
                    Status = s.Status,
                    SubmittedDate = DateTime.Today.AddDays(-s.DaysAgo),
                    Reviewer = s.Status == ApplicationStatuses.UnderReview ? "admin@imsb.tn.gov.in" : null,
                    ReviewStartedAt = s.Status == ApplicationStatuses.UnderReview ? DateTime.UtcNow.AddDays(-1) : null,
                    CreatedBy = "seed"
                });
            }
            context.SaveChanges();
        }
    }
}
