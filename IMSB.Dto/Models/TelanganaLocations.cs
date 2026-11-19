namespace IMSB.Dto.Models
{
    /// <summary>
    /// Seed data for the Districts table: the 33 districts of Telangana (fixed ids so the seed is stable across environments;
    /// do not renumber them). The mandals that used to be seeded here went with the Mandals table (RemoveMandalsAddEmployeeId).
    /// </summary>
    public static class TelanganaLocations
    {
        public static readonly (int Id, string Name)[] Districts =
        {
            (1, "Adilabad"),
            (2, "Bhadradri Kothagudem"),
            (3, "Hanumakonda"),
            (4, "Hyderabad"),
            (5, "Jagtial"),
            (6, "Jangaon"),
            (7, "Jayashankar Bhupalpally"),
            (8, "Jogulamba Gadwal"),
            (9, "Kamareddy"),
            (10, "Karimnagar"),
            (11, "Khammam"),
            (12, "Kumuram Bheem Asifabad"),
            (13, "Mahabubabad"),
            (14, "Mahabubnagar"),
            (15, "Mancherial"),
            (16, "Medak"),
            (17, "Medchal–Malkajgiri"),
            (18, "Mulugu"),
            (19, "Nagarkurnool"),
            (20, "Nalgonda"),
            (21, "Narayanpet"),
            (22, "Nirmal"),
            (23, "Nizamabad"),
            (24, "Peddapalli"),
            (25, "Rajanna Sircilla"),
            (26, "Ranga Reddy"),
            (27, "Sangareddy"),
            (28, "Siddipet"),
            (29, "Suryapet"),
            (30, "Vikarabad"),
            (31, "Wanaparthy"),
            (32, "Warangal"),
            (33, "Yadadri Bhuvanagiri"),
        };
    }
}
