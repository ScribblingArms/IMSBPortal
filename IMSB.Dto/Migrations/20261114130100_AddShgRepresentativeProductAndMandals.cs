using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddShgRepresentativeProductAndMandals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // District/Mandal free text becomes DistrictId/MandalId; existing rows cannot be converted automatically
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [SHGDetails]) " +
                "THROW 50001, 'SHGDetails has rows: their District/Mandal text cannot be converted to DistrictId/MandalId. Move or delete them, then run the migration again.', 1;");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_District",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "District",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "Mandal",
                table: "SHGDetails");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "SHGDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MandalId",
                table: "SHGDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceMax",
                table: "SHGDetails",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceMin",
                table: "SHGDetails",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductCategoryId",
                table: "SHGDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDescription",
                table: "SHGDetails",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductPhotos",
                table: "SHGDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductSavedAt",
                table: "SHGDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "SHGDetails",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionCapacity",
                table: "SHGDetails",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepAddress",
                table: "SHGDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepAge",
                table: "SHGDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepAltMobile",
                table: "SHGDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepEmail",
                table: "SHGDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepGuardianName",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepMobile",
                table: "SHGDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepName",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepPinCode",
                table: "SHGDetails",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepRole",
                table: "SHGDetails",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepSavedAt",
                table: "SHGDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mandals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistrictId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mandals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mandals_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Districts",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Adilabad" },
                    { 2, "Bhadradri Kothagudem" },
                    { 3, "Hanumakonda" },
                    { 4, "Hyderabad" },
                    { 5, "Jagtial" },
                    { 6, "Jangaon" },
                    { 7, "Jayashankar Bhupalpally" },
                    { 8, "Jogulamba Gadwal" },
                    { 9, "Kamareddy" },
                    { 10, "Karimnagar" },
                    { 11, "Khammam" },
                    { 12, "Kumuram Bheem Asifabad" },
                    { 13, "Mahabubabad" },
                    { 14, "Mahabubnagar" },
                    { 15, "Mancherial" },
                    { 16, "Medak" },
                    { 17, "Medchal–Malkajgiri" },
                    { 18, "Mulugu" },
                    { 19, "Nagarkurnool" },
                    { 20, "Nalgonda" },
                    { 21, "Narayanpet" },
                    { 22, "Nirmal" },
                    { 23, "Nizamabad" },
                    { 24, "Peddapalli" },
                    { 25, "Rajanna Sircilla" },
                    { 26, "Ranga Reddy" },
                    { 27, "Sangareddy" },
                    { 28, "Siddipet" },
                    { 29, "Suryapet" },
                    { 30, "Vikarabad" },
                    { 31, "Wanaparthy" },
                    { 32, "Warangal" },
                    { 33, "Yadadri Bhuvanagiri" }
                });

            migrationBuilder.InsertData(
                table: "Mandals",
                columns: new[] { "Id", "DistrictId", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Adilabad Rural" },
                    { 2, 1, "Adilabad Urban" },
                    { 3, 1, "Bazarhatnoor" },
                    { 4, 1, "Bela" },
                    { 5, 1, "Bheempoor" },
                    { 6, 1, "Boath" },
                    { 7, 1, "Gadiguda" },
                    { 8, 1, "Gudihathnur" },
                    { 9, 1, "Ichoda" },
                    { 10, 1, "Inderavelly" },
                    { 11, 1, "Jainad" },
                    { 12, 1, "Mavala" },
                    { 13, 1, "Narnoor" },
                    { 14, 1, "Neradigonda" },
                    { 15, 1, "Sirikonda" },
                    { 16, 1, "Talamadagu" },
                    { 17, 1, "Tamsi" },
                    { 18, 1, "Utnoor" },
                    { 19, 2, "Allapalli" },
                    { 20, 2, "Annapureddypally" },
                    { 21, 2, "Aswapuram" },
                    { 22, 2, "Aswaraopeta" },
                    { 23, 2, "Bhadrachalam" },
                    { 24, 2, "Burgampahad" },
                    { 25, 2, "Chandrugonda" },
                    { 26, 2, "Cherla" },
                    { 27, 2, "Chunchupally" },
                    { 28, 2, "Dammapeta" },
                    { 29, 2, "Dummugudem" },
                    { 30, 2, "Gundala" },
                    { 31, 2, "Julurpad" },
                    { 32, 2, "Karakagudem" },
                    { 33, 2, "Kothagudem" },
                    { 34, 2, "Laxmidevipalli" },
                    { 35, 2, "Manuguru" },
                    { 36, 2, "Mulakalapalle" },
                    { 37, 2, "Palvancha" },
                    { 38, 2, "Pinapaka" },
                    { 39, 2, "Sujathanagar" },
                    { 40, 2, "Tekulapalle" },
                    { 41, 2, "Yellandu" },
                    { 42, 3, "Atmakur" },
                    { 43, 3, "Bheemadevarapalle" },
                    { 44, 3, "Damera" },
                    { 45, 3, "Dharmasagar" },
                    { 46, 3, "Elkathurthy" },
                    { 47, 3, "Hanamkonda" },
                    { 48, 3, "Hasanparthy" },
                    { 49, 3, "Inavole" },
                    { 50, 3, "Kamalapur" },
                    { 51, 3, "Kazipet" },
                    { 52, 3, "Nadikuda" },
                    { 53, 3, "Parkal" },
                    { 54, 3, "Shayampet" },
                    { 55, 3, "Velair" },
                    { 56, 4, "Amberpet" },
                    { 57, 4, "Ameerpet" },
                    { 58, 4, "Asif Nagar" },
                    { 59, 4, "Bahadurpura" },
                    { 60, 4, "Bandlaguda" },
                    { 61, 4, "Charminar" },
                    { 62, 4, "Golkonda" },
                    { 63, 4, "Himayathnagar" },
                    { 64, 4, "Khairtabad" },
                    { 65, 4, "Maredpally" },
                    { 66, 4, "Musheerabad" },
                    { 67, 4, "Nampally" },
                    { 68, 4, "Saidabad" },
                    { 69, 4, "Secunderabad" },
                    { 70, 4, "Shaikpet" },
                    { 71, 4, "Tirumalgiri" },
                    { 72, 5, "Beerpur" },
                    { 73, 5, "Buggaram" },
                    { 74, 5, "Dharmapuri" },
                    { 75, 5, "Gollapalle" },
                    { 76, 5, "Ibrahimpatnam" },
                    { 77, 5, "Jagtial" },
                    { 78, 5, "Jagtial Rural" },
                    { 79, 5, "Kathlapur" },
                    { 80, 5, "Kodimial" },
                    { 81, 5, "Korutla" },
                    { 82, 5, "Mallapur" },
                    { 83, 5, "Mallial" },
                    { 84, 5, "Medipalle" },
                    { 85, 5, "Metpalli" },
                    { 86, 5, "Pegadapalle" },
                    { 87, 5, "Raikal" },
                    { 88, 5, "Sarangapur" },
                    { 89, 5, "Velgatoor" },
                    { 90, 6, "Bachannapeta" },
                    { 91, 6, "Chilpur" },
                    { 92, 6, "Devaruppala" },
                    { 93, 6, "Jangaon" },
                    { 94, 6, "Kodakandla" },
                    { 95, 6, "Lingalaghanpur" },
                    { 96, 6, "Narmetta" },
                    { 97, 6, "Palakurthi" },
                    { 98, 6, "Raghunathapalle" },
                    { 99, 6, "Station Ghanpur" },
                    { 100, 6, "Tharigoppula" },
                    { 101, 6, "Zaffergadh" },
                    { 102, 7, "Bhupalpalle" },
                    { 103, 7, "Chityal" },
                    { 104, 7, "Ghanpur" },
                    { 105, 7, "Kataram" },
                    { 106, 7, "Maha Mutharam" },
                    { 107, 7, "Mahadevpur" },
                    { 108, 7, "Malharrao" },
                    { 109, 7, "Mogullapalle" },
                    { 110, 7, "Palimela" },
                    { 111, 7, "Regonda" },
                    { 112, 7, "Tekumatla" },
                    { 113, 8, "Aiza" },
                    { 114, 8, "Alampur" },
                    { 115, 8, "Dharur" },
                    { 116, 8, "Gadwal" },
                    { 117, 8, "Ghattu" },
                    { 118, 8, "Itikyal" },
                    { 119, 8, "Kaloor_Timmanadoddi" },
                    { 120, 8, "Maldakal" },
                    { 121, 8, "Manopad" },
                    { 122, 8, "Rajoli" },
                    { 123, 8, "Undavelli" },
                    { 124, 8, "Waddepalle" },
                    { 125, 9, "Banswada" },
                    { 126, 9, "Bhiknoor" },
                    { 127, 9, "Bibipet" },
                    { 128, 9, "Bichkunda" },
                    { 129, 9, "Birkoor" },
                    { 130, 9, "Domakonda" },
                    { 131, 9, "Dongli" },
                    { 132, 9, "Gandhari" },
                    { 133, 9, "Jukkal" },
                    { 134, 9, "Kamareddy" },
                    { 135, 9, "Lingampet" },
                    { 136, 9, "Machareddy" },
                    { 137, 9, "Madnur" },
                    { 138, 9, "Naga Reddipet" },
                    { 139, 9, "Nasurullabad" },
                    { 140, 9, "Nizamsagar" },
                    { 141, 9, "Palvancha" },
                    { 142, 9, "Pedda Kodapgal" },
                    { 143, 9, "Pitlam" },
                    { 144, 9, "Rajampet" },
                    { 145, 9, "Ramareddy" },
                    { 146, 9, "Sadasivanagar" },
                    { 147, 9, "Tadwai" },
                    { 148, 9, "Yellareddy" },
                    { 149, 10, "Chigurumamidi" },
                    { 150, 10, "Choppadandi" },
                    { 151, 10, "Ellandakunta" },
                    { 152, 10, "Gangadhara" },
                    { 153, 10, "Ganneruvaram" },
                    { 154, 10, "Huzurabad" },
                    { 155, 10, "Jammikunta" },
                    { 156, 10, "Karimnagar" },
                    { 157, 10, "Karimnagar (Rural-I)" },
                    { 158, 10, "Karimnagar (Rural-II)" },
                    { 159, 10, "Manakondur" },
                    { 160, 10, "Ramadugu" },
                    { 161, 10, "Shankarapatnam" },
                    { 162, 10, "Thimmapur" },
                    { 163, 10, "V_Saidapur" },
                    { 164, 10, "Veenavanka" },
                    { 165, 11, "Bonakal" },
                    { 166, 11, "Chinthakani" },
                    { 167, 11, "Enkuru" },
                    { 168, 11, "Kalluru" },
                    { 169, 11, "Kamepalle" },
                    { 170, 11, "Khammam_Rural" },
                    { 171, 11, "Khammam_Urban" },
                    { 172, 11, "Konijerla" },
                    { 173, 11, "Kusumanchi" },
                    { 174, 11, "Madhira" },
                    { 175, 11, "Mudigonda" },
                    { 176, 11, "Nelakondapalle" },
                    { 177, 11, "Penuballi" },
                    { 178, 11, "Raghunadhapalem" },
                    { 179, 11, "Sathupalli" },
                    { 180, 11, "Singareni" },
                    { 181, 11, "Thallada" },
                    { 182, 11, "Thirumalayapalem" },
                    { 183, 11, "Vemsoor" },
                    { 184, 11, "Wyra" },
                    { 185, 11, "Yerrupalem" },
                    { 186, 12, "Asifabad" },
                    { 187, 12, "Bejjur" },
                    { 188, 12, "Chintalamanepally" },
                    { 189, 12, "Dahegaon" },
                    { 190, 12, "Jainoor" },
                    { 191, 12, "Kagaznagar" },
                    { 192, 12, "Kerameri" },
                    { 193, 12, "Kouthala" },
                    { 194, 12, "Lingapur" },
                    { 195, 12, "Penchikalpet" },
                    { 196, 12, "Rebbena" },
                    { 197, 12, "Sirpur_T" },
                    { 198, 12, "Sirpur_U" },
                    { 199, 12, "Tiryani" },
                    { 200, 12, "Wankidi" },
                    { 201, 13, "Bayyaram" },
                    { 202, 13, "Chinnagudur" },
                    { 203, 13, "Danthalapalle" },
                    { 204, 13, "Dornakal" },
                    { 205, 13, "Ganagavaram" },
                    { 206, 13, "Garla" },
                    { 207, 13, "Gudur" },
                    { 208, 13, "Inugurthy" },
                    { 209, 13, "Kesamudram" },
                    { 210, 13, "Kothaguda" },
                    { 211, 13, "Kuravi" },
                    { 212, 13, "Mahabubabad" },
                    { 213, 13, "Maripeda" },
                    { 214, 13, "Narsimhulapet" },
                    { 215, 13, "Nellikudur" },
                    { 216, 13, "Peddavangara" },
                    { 217, 13, "Seerole" },
                    { 218, 13, "Thorrur" },
                    { 219, 14, "Addakal" },
                    { 220, 14, "Balanagar" },
                    { 221, 14, "Bhoothpur" },
                    { 222, 14, "Chinna_Chintha_Kunta" },
                    { 223, 14, "Devarkadara" },
                    { 224, 14, "Gandeed" },
                    { 225, 14, "Hanwada" },
                    { 226, 14, "Jadcherla" },
                    { 227, 14, "Koilkonda" },
                    { 228, 14, "Mahabubnagar_Rural" },
                    { 229, 14, "Mahabubnagar_Urban" },
                    { 230, 14, "Midjil" },
                    { 231, 14, "Mohammadabad" },
                    { 232, 14, "Musapet" },
                    { 233, 14, "Nawabpet" },
                    { 234, 14, "Rajapur" },
                    { 235, 15, "Bellampally Mandal" },
                    { 236, 15, "Bheemaram" },
                    { 237, 15, "Bheemini" },
                    { 238, 15, "Chennur" },
                    { 239, 15, "Dandepally" },
                    { 240, 15, "Hajipur" },
                    { 241, 15, "Jaipur" },
                    { 242, 15, "Jannaram" },
                    { 243, 15, "Kannepally" },
                    { 244, 15, "Kasipet" },
                    { 245, 15, "Kotapally" },
                    { 246, 15, "Luxettipet" },
                    { 247, 15, "Mancherial" },
                    { 248, 15, "Mandamarri" },
                    { 249, 15, "Naspur" },
                    { 250, 15, "Nennel" },
                    { 251, 15, "Tandur" },
                    { 252, 15, "Vempally" },
                    { 253, 16, "Alladurg" },
                    { 254, 16, "Chegunta" },
                    { 255, 16, "Chilipched" },
                    { 256, 16, "Havelighanpur" },
                    { 257, 16, "Kowdipalle" },
                    { 258, 16, "Kulcharam" },
                    { 259, 16, "Manoharabad" },
                    { 260, 16, "Masaipet" },
                    { 261, 16, "Medak" },
                    { 262, 16, "Narsapur" },
                    { 263, 16, "Narsingi" },
                    { 264, 16, "Nizampet" },
                    { 265, 16, "Papannapet" },
                    { 266, 16, "Ramayampet" },
                    { 267, 16, "Regode" },
                    { 268, 16, "Shankarampet_A" },
                    { 269, 16, "Shankarampet_R" },
                    { 270, 16, "Shivampet" },
                    { 271, 16, "Tekmal" },
                    { 272, 16, "Tupran" },
                    { 273, 16, "Yeldurthy" },
                    { 274, 17, "Alwal" },
                    { 275, 17, "Bachupally" },
                    { 276, 17, "Balanagar" },
                    { 277, 17, "Dundigal Gandimaisamma" },
                    { 278, 17, "Ghatkesar" },
                    { 279, 17, "Kapra" },
                    { 280, 17, "Keesara" },
                    { 281, 17, "Kukatpally" },
                    { 282, 17, "Malkajgiri" },
                    { 283, 17, "Medchal" },
                    { 284, 17, "Medipally, Telangana" },
                    { 285, 17, "Muduchintalpalli" },
                    { 286, 17, "Quthbullapur" },
                    { 287, 17, "Shamirpet" },
                    { 288, 17, "Uppal" },
                    { 289, 18, "Eturnagaram" },
                    { 290, 18, "Govindaraopet" },
                    { 291, 18, "Kannaigudem" },
                    { 292, 18, "Mangapet" },
                    { 293, 18, "Mulugu" },
                    { 294, 18, "Sammakka Saralamma Tadvai" },
                    { 295, 18, "Venkatapur" },
                    { 296, 18, "Venkatapuram" },
                    { 297, 18, "Wazeed" },
                    { 298, 19, "Achampet" },
                    { 299, 19, "Amrabad" },
                    { 300, 19, "Balmoor" },
                    { 301, 19, "Bijinapalle" },
                    { 302, 19, "Charakonda" },
                    { 303, 19, "Kalwakurthy" },
                    { 304, 19, "Kodair" },
                    { 305, 19, "Kollapur" },
                    { 306, 19, "Lingal" },
                    { 307, 19, "Nagarkurnool" },
                    { 308, 19, "Padra" },
                    { 309, 19, "Peddakothapalle" },
                    { 310, 19, "Pentlavelli" },
                    { 311, 19, "Tadoor" },
                    { 312, 19, "Telkapalle" },
                    { 313, 19, "Thimmajipeta" },
                    { 314, 19, "Uppunuthala" },
                    { 315, 19, "Urkonda" },
                    { 316, 19, "Vangoor" },
                    { 317, 19, "Veldanda" },
                    { 318, 20, "Adavi devula palli" },
                    { 319, 20, "Anumula_Haliya" },
                    { 320, 20, "Chandampet" },
                    { 321, 20, "Chandur" },
                    { 322, 20, "Chinthapalle" },
                    { 323, 20, "Chityala" },
                    { 324, 20, "Damaracherla" },
                    { 325, 20, "Devarakonda" },
                    { 326, 20, "Gundlapalle" },
                    { 327, 20, "Gurrampode" },
                    { 328, 20, "Kangal" },
                    { 329, 20, "Kattangoor" },
                    { 330, 20, "Kethepalle" },
                    { 331, 20, "Kondamallapally" },
                    { 332, 20, "Madugulapally" },
                    { 333, 20, "Marriguda" },
                    { 334, 20, "Miryalaguda" },
                    { 335, 20, "Munugode" },
                    { 336, 20, "Nakrekal" },
                    { 337, 20, "Nalgonda" },
                    { 338, 20, "Nampalle" },
                    { 339, 20, "Narketpalle" },
                    { 340, 20, "Neredugommu" },
                    { 341, 20, "Nidamanur" },
                    { 342, 20, "Pedda_adiserlapalle" },
                    { 343, 20, "Peddavoora" },
                    { 344, 20, "Saligouraram" },
                    { 345, 20, "Thipparthi" },
                    { 346, 20, "Thripuraram" },
                    { 347, 20, "Tirumalagiri_Sagar" },
                    { 348, 20, "Vemulapalle" },
                    { 349, 21, "Damaragidda" },
                    { 350, 21, "Dhanwada" },
                    { 351, 21, "Gundumal" },
                    { 352, 21, "Kosgi" },
                    { 353, 21, "Kottha pally" },
                    { 354, 21, "Krishna" },
                    { 355, 21, "Maddur" },
                    { 356, 21, "Maganoor" },
                    { 357, 21, "Makthal" },
                    { 358, 21, "Marikal" },
                    { 359, 21, "Narayanpet" },
                    { 360, 21, "Narva" },
                    { 361, 21, "Utkoor" },
                    { 362, 22, "Basar" },
                    { 363, 22, "Bhainsa" },
                    { 364, 22, "Dastuarabad" },
                    { 365, 22, "Dilawarpur" },
                    { 366, 22, "Kaddampeddur" },
                    { 367, 22, "Khanapur" },
                    { 368, 22, "Kubeer" },
                    { 369, 22, "Kuntala" },
                    { 370, 22, "Laxmanchanda" },
                    { 371, 22, "Lokeshwaram" },
                    { 372, 22, "Mamada" },
                    { 373, 22, "Mudhole" },
                    { 374, 22, "Narsapur_G" },
                    { 375, 22, "Nirmal Rural" },
                    { 376, 22, "Nirmal Urban" },
                    { 377, 22, "Pembi" },
                    { 378, 22, "Sarangapur" },
                    { 379, 22, "Soan" },
                    { 380, 22, "Tanoor" },
                    { 381, 23, "Aloor" },
                    { 382, 23, "Armur" },
                    { 383, 23, "Balkonda" },
                    { 384, 23, "Bheemgal" },
                    { 385, 23, "Bodhan" },
                    { 386, 23, "Chandur" },
                    { 387, 23, "Dharpalle" },
                    { 388, 23, "Dichpalle" },
                    { 389, 23, "Donkeshwar" },
                    { 390, 23, "Indalwai" },
                    { 391, 23, "Jakranpalle" },
                    { 392, 23, "Kammarpalle" },
                    { 393, 23, "Kotgiri" },
                    { 394, 23, "Makloor" },
                    { 395, 23, "Mendora" },
                    { 396, 23, "Mortad" },
                    { 397, 23, "Mosra" },
                    { 398, 23, "Mugpal" },
                    { 399, 23, "Mupkal" },
                    { 400, 23, "Nandipet" },
                    { 401, 23, "Navipet" },
                    { 402, 23, "Nizamabad_North" },
                    { 403, 23, "Nizamabad_Rural" },
                    { 404, 23, "Nizamabad_South" },
                    { 405, 23, "Pothangal" },
                    { 406, 23, "Ranjal" },
                    { 407, 23, "Rudrur" },
                    { 408, 23, "Saloora" },
                    { 409, 23, "Sirkonda" },
                    { 410, 23, "Vailpur" },
                    { 411, 23, "Varni" },
                    { 412, 23, "Yedapalle" },
                    { 413, 23, "Yergatla" },
                    { 414, 24, "Anthergoan" },
                    { 415, 24, "Dharmaram" },
                    { 416, 24, "Elgaid" },
                    { 417, 24, "Julapalle" },
                    { 418, 24, "Kamanpur" },
                    { 419, 24, "Manthani" },
                    { 420, 24, "Mutharam" },
                    { 421, 24, "Odela" },
                    { 422, 24, "Palakurthy" },
                    { 423, 24, "Peddapalli" },
                    { 424, 24, "Ramagiri" },
                    { 425, 24, "Ramagundam" },
                    { 426, 24, "Srirampur" },
                    { 427, 24, "Sultanabad" },
                    { 428, 25, "Boinpalle" },
                    { 429, 25, "Chandurthi" },
                    { 430, 25, "Ellanthakunta" },
                    { 431, 25, "Gambhiraopeta" },
                    { 432, 25, "Konaraopeta" },
                    { 433, 25, "Mustabad" },
                    { 434, 25, "Rudrangi" },
                    { 435, 25, "Sircilla" },
                    { 436, 25, "Thangallapalle" },
                    { 437, 25, "Veernapalle" },
                    { 438, 25, "Vemulawada" },
                    { 439, 25, "Vemulawada_Rural" },
                    { 440, 25, "Yellareddypeta" },
                    { 441, 26, "Abdullapurmet" },
                    { 442, 26, "Amangal" },
                    { 443, 26, "Balapur" },
                    { 444, 26, "Chevella" },
                    { 445, 26, "Farooqnagar" },
                    { 446, 26, "Gandipet" },
                    { 447, 26, "Hayathnagar" },
                    { 448, 26, "Ibrahimpatnam" },
                    { 449, 26, "Jilled Chowdergudem" },
                    { 450, 26, "Kadthal" },
                    { 451, 26, "Kandukur" },
                    { 452, 26, "Keshampeta" },
                    { 453, 26, "Kondurg" },
                    { 454, 26, "Kothur" },
                    { 455, 26, "Madgul" },
                    { 456, 26, "Maheshwaram" },
                    { 457, 26, "Manchal" },
                    { 458, 26, "Moinabad" },
                    { 459, 26, "Nandigam" },
                    { 460, 26, "Rajendranagar" },
                    { 461, 26, "Saroornagar" },
                    { 462, 26, "Serilingampally" },
                    { 463, 26, "Shabad" },
                    { 464, 26, "Shadnagar" },
                    { 465, 26, "Shamshabad" },
                    { 466, 26, "Shankarpalle" },
                    { 467, 26, "Talakonapally" },
                    { 468, 26, "Yacharam" },
                    { 469, 27, "Ameenpur" },
                    { 470, 27, "Andole" },
                    { 471, 27, "Gummadidala" },
                    { 472, 27, "Hathnoora" },
                    { 473, 27, "Jharasangam" },
                    { 474, 27, "Jinnaram" },
                    { 475, 27, "Kalher" },
                    { 476, 27, "Kandi" },
                    { 477, 27, "Kangti" },
                    { 478, 27, "Kohir" },
                    { 479, 27, "Kondapur" },
                    { 480, 27, "Manoor" },
                    { 481, 27, "Mogudampally" },
                    { 482, 27, "Munipally" },
                    { 483, 27, "Nagilgidda" },
                    { 484, 27, "Narayankhed" },
                    { 485, 27, "Nyalkal" },
                    { 486, 27, "Patancheru" },
                    { 487, 27, "Pulkal" },
                    { 488, 27, "Raikode" },
                    { 489, 27, "Ramchandrapuram" },
                    { 490, 27, "Sadasivpet" },
                    { 491, 27, "Sangareddy" },
                    { 492, 27, "Sirgapoor" },
                    { 493, 27, "Vatpally" },
                    { 494, 27, "Zaheerabad" },
                    { 495, 28, "Akberpet Bhoompally" },
                    { 496, 28, "Akkannapet" },
                    { 497, 28, "Bejjanki" },
                    { 498, 28, "Cherial" },
                    { 499, 28, "Chinnakodur" },
                    { 500, 28, "Dhoolmitta" },
                    { 501, 28, "Doulthabad" },
                    { 502, 28, "Dubbak" },
                    { 503, 28, "Gajwel" },
                    { 504, 28, "Husnabad" },
                    { 505, 28, "Jagdevpur" },
                    { 506, 28, "Koheda" },
                    { 507, 28, "Komuravelli" },
                    { 508, 28, "Kondapak" },
                    { 509, 28, "Kukunoorpally" },
                    { 510, 28, "Maddur" },
                    { 511, 28, "Markook" },
                    { 512, 28, "Mirdoddi" },
                    { 513, 28, "Mulugu" },
                    { 514, 28, "Nangnoor" },
                    { 515, 28, "Narayanaraopet" },
                    { 516, 28, "Raipole" },
                    { 517, 28, "Siddipet (Rural)" },
                    { 518, 28, "Siddipet (Urban)" },
                    { 519, 28, "Thoguta" },
                    { 520, 28, "Wargal" },
                    { 521, 29, "Ananthagiri" },
                    { 522, 29, "Atmakur (s)" },
                    { 523, 29, "Chilkur" },
                    { 524, 29, "Chivvemla" },
                    { 525, 29, "Garidepally" },
                    { 526, 29, "Huzurnagar" },
                    { 527, 29, "Jajireddygudem" },
                    { 528, 29, "Kodad" },
                    { 529, 29, "Maddirala" },
                    { 530, 29, "Mallareddygudem" },
                    { 531, 29, "Mattampally" },
                    { 532, 29, "Mellachervu" },
                    { 533, 29, "Mothey" },
                    { 534, 29, "Munagala" },
                    { 535, 29, "Nadigudem" },
                    { 536, 29, "Nagaram" },
                    { 537, 29, "Neredcherla" },
                    { 538, 29, "Nuthankal" },
                    { 539, 29, "Palakeedu" },
                    { 540, 29, "Penpahad" },
                    { 541, 29, "Suryapet" },
                    { 542, 29, "Thirumalagiri" },
                    { 543, 29, "Thungathurthy" },
                    { 544, 30, "Bantwaram" },
                    { 545, 30, "Basheerabad" },
                    { 546, 30, "Bommaraspet" },
                    { 547, 30, "Dharur" },
                    { 548, 30, "Doma" },
                    { 549, 30, "Doulthabad" },
                    { 550, 30, "Kodangal" },
                    { 551, 30, "Kotepally" },
                    { 552, 30, "Kulkacherla" },
                    { 553, 30, "Marpalle" },
                    { 554, 30, "Mominpet" },
                    { 555, 30, "Nawabpet" },
                    { 556, 30, "Pargi" },
                    { 557, 30, "Peddemul" },
                    { 558, 30, "Pudur" },
                    { 559, 30, "Tandur" },
                    { 560, 30, "Vikarabad" },
                    { 561, 30, "Yelal" },
                    { 562, 31, "Amarchinta" },
                    { 563, 31, "Atmakur" },
                    { 564, 31, "Chinnambavi" },
                    { 565, 31, "Ghanpur (Khilla)" },
                    { 566, 31, "Gopalpeta" },
                    { 567, 31, "Kothakota" },
                    { 568, 31, "Madanapur" },
                    { 569, 31, "Pangal" },
                    { 570, 31, "Pebbair" },
                    { 571, 31, "Peddamandadi" },
                    { 572, 31, "Revally" },
                    { 573, 31, "Srirangapur" },
                    { 574, 31, "Veepanagandla" },
                    { 575, 31, "Wanaparthy" },
                    { 576, 32, "Chennaraopet" },
                    { 577, 32, "Duggondi" },
                    { 578, 32, "Geesugonda" },
                    { 579, 32, "Khanapur" },
                    { 580, 32, "Khila Warangal" },
                    { 581, 32, "Nallabelly" },
                    { 582, 32, "Narsampet" },
                    { 583, 32, "Nekkonda" },
                    { 584, 32, "Parvathagiri" },
                    { 585, 32, "Rayaparthy" },
                    { 586, 32, "Sangem" },
                    { 587, 32, "Warangal" },
                    { 588, 32, "Wardhannapet" },
                    { 589, 33, "Addaguduru" },
                    { 590, 33, "Alair" },
                    { 591, 33, "Atmakur (M)" },
                    { 592, 33, "Bhongir" },
                    { 593, 33, "Bhoodan Pochampally" },
                    { 594, 33, "Bibinagar" },
                    { 595, 33, "Bommalaramaram" },
                    { 596, 33, "Choutuppal" },
                    { 597, 33, "Gundala" },
                    { 598, 33, "Motakondur" },
                    { 599, 33, "Mothkur" },
                    { 600, 33, "Narayanpur" },
                    { 601, 33, "Rajapet" },
                    { 602, 33, "Ramannapet" },
                    { 603, 33, "Turkapally" },
                    { 604, 33, "Valigonda" },
                    { 605, 33, "Yadagirigutta" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_DistrictId",
                table: "SHGDetails",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_MandalId",
                table: "SHGDetails",
                column: "MandalId");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_ProductCategoryId",
                table: "SHGDetails",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_RepMobile",
                table: "SHGDetails",
                column: "RepMobile");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_Name",
                table: "Districts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mandals_DistrictId_Name",
                table: "Mandals",
                columns: new[] { "DistrictId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SHGDetails_Districts_DistrictId",
                table: "SHGDetails",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SHGDetails_Mandals_MandalId",
                table: "SHGDetails",
                column: "MandalId",
                principalTable: "Mandals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SHGDetails_product_categories_ProductCategoryId",
                table: "SHGDetails",
                column: "ProductCategoryId",
                principalTable: "product_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SHGDetails_Districts_DistrictId",
                table: "SHGDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_SHGDetails_Mandals_MandalId",
                table: "SHGDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_SHGDetails_product_categories_ProductCategoryId",
                table: "SHGDetails");

            migrationBuilder.DropTable(
                name: "Mandals");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_DistrictId",
                table: "SHGDetails");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_MandalId",
                table: "SHGDetails");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_ProductCategoryId",
                table: "SHGDetails");

            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_RepMobile",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "MandalId",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "PriceMax",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "PriceMin",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductCategoryId",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductDescription",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductPhotos",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductSavedAt",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ProductionCapacity",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepAddress",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepAge",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepAltMobile",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepEmail",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepGuardianName",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepMobile",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepName",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepPinCode",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepRole",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "RepSavedAt",
                table: "SHGDetails");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "SHGDetails",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Mandal",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_District",
                table: "SHGDetails",
                column: "District");
        }
    }
}
