using IMSB.Dto.Migrations.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Every IMSB table gets an int IDENTITY key instead of a Guid, and every link between them an int.
    /// user_sessions and password_reset_tokens keep their random Guid ids (they must not be guessable); their LoginId is int.
    /// SQL Server cannot change a uniqueidentifier key to IDENTITY in place, so the 21 tables are dropped and created
    /// again EMPTY (all staff, logins, stalls, attendance... rows are deleted). Seeded again: leave types, product
    /// categories and the 7 shifts; demo logins/stalls/products come back when SeedDemoData is on.
    /// The SERP.UpdateAttendanceManagement trigger is re-created for the int keys. Not reversible: restore a backup instead.
    /// </summary>
    public partial class UseIntIdentityKeys : Migration
    {
        // dependents before the tables they point to
        private static readonly string[] Tables =
        {
            "password_reset_tokens", "user_sessions",
            "stall_reallocations", "stalls", "artisan_applications", "artisans",
            "sale_items", "sales_entries", "stock_movements", "products", "product_categories",
            "attendance_records", "ot_assignments", "leave_entitlements", "staff_leave_entries", "pay_bill_entries",
            "staff", "departments", "Shift", "leave_types", "logins",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.DropTable(name: table);

            // ── the tables again, with int IDENTITY keys (generated from the model) ──
            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "logins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Admin"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pay_bill_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonthDays = table.Column<int>(type: "int", nullable: false),
                    PaidDays = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BasicPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Allowances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    OvertimeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_bill_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SoldAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shift",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shift", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AadhaarHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AadhaarLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    EmployeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StaffCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JoinDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_logins_LoginId",
                        column: x => x.LoginId,
                        principalTable: "logins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPersistent = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_logins_LoginId",
                        column: x => x.LoginId,
                        principalTable: "logins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artisans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArtisanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AadhaarHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AadhaarLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: false),
                    Skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "date", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: true),
                    ToDate = table.Column<DateTime>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artisans_product_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    StallCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    MinStock = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_product_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LeaveWithoutPay = table.Column<bool>(type: "bit", nullable: false),
                    InTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    WorkedHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_records_staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leave_entitlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    StaffCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AnnualQuota = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    AttendanceLeaveType = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_entitlements_staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ot_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    FromDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OtHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ot_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ot_assignments_staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_leave_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_leave_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_leave_entries_staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artisan_applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AadhaarHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AadhaarLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: false),
                    Skills = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "date", nullable: false),
                    FromDate = table.Column<DateTime>(type: "date", nullable: true),
                    ToDate = table.Column<DateTime>(type: "date", nullable: true),
                    Reviewer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecisionRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArtisanId = table.Column<int>(type: "int", nullable: true),
                    ArtisanCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StallCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisan_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artisan_applications_artisans_ArtisanId",
                        column: x => x.ArtisanId,
                        principalTable: "artisans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_artisan_applications_product_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stalls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StallCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Block = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    ArtisanId = table.Column<int>(type: "int", nullable: true),
                    AllottedDate = table.Column<DateTime>(type: "date", nullable: true),
                    AgreementEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stalls_artisans_ArtisanId",
                        column: x => x.ArtisanId,
                        principalTable: "artisans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stalls_product_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "product_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesEntryId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StallCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_items_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sale_items_sales_entries_SalesEntryId",
                        column: x => x.SalesEntryId,
                        principalTable: "sales_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stall_reallocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StallId = table.Column<int>(type: "int", nullable: false),
                    StallCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentArtisanId = table.Column<int>(type: "int", nullable: true),
                    CurrentArtisanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewArtisanId = table.Column<int>(type: "int", nullable: true),
                    NewArtisanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecisionRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stall_reallocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stall_reallocations_artisans_CurrentArtisanId",
                        column: x => x.CurrentArtisanId,
                        principalTable: "artisans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stall_reallocations_artisans_NewArtisanId",
                        column: x => x.NewArtisanId,
                        principalTable: "artisans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stall_reallocations_stalls_StallId",
                        column: x => x.StallId,
                        principalTable: "stalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "leave_types",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Casual" },
                    { 2, "Earned" },
                    { 3, "Sick" }
                });

            migrationBuilder.InsertData(
                table: "product_categories",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Textiles", 1 },
                    { 2, "Handicrafts", 2 },
                    { 3, "Jewellery", 3 },
                    { 4, "Pottery", 4 },
                    { 5, "Paintings", 5 },
                    { 6, "Herbal", 6 },
                    { 7, "Food & Snacks", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_AadhaarHash",
                table: "artisan_applications",
                column: "AadhaarHash");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_ApplicationNo",
                table: "artisan_applications",
                column: "ApplicationNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_ArtisanId",
                table: "artisan_applications",
                column: "ArtisanId");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_CategoryId",
                table: "artisan_applications",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_Mobile",
                table: "artisan_applications",
                column: "Mobile");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_Status",
                table: "artisan_applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_applications_SubmittedDate",
                table: "artisan_applications",
                column: "SubmittedDate");

            migrationBuilder.CreateIndex(
                name: "IX_artisans_AadhaarHash",
                table: "artisans",
                column: "AadhaarHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_ArtisanCode",
                table: "artisans",
                column: "ArtisanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_CategoryId",
                table: "artisans",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_artisans_Mobile",
                table: "artisans",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisans_Status",
                table: "artisans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_AttendanceDate",
                table: "attendance_records",
                column: "AttendanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_StaffId_AttendanceDate",
                table: "attendance_records",
                columns: new[] { "StaffId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_Status",
                table: "attendance_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_departments_Name",
                table: "departments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_StaffId_LeaveType_Year",
                table: "leave_entitlements",
                columns: new[] { "StaffId", "LeaveType", "Year" },
                unique: true,
                filter: "[StaffId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_StaffId_Year",
                table: "leave_entitlements",
                columns: new[] { "StaffId", "Year" },
                unique: true,
                filter: "[AttendanceLeaveType] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_Name",
                table: "leave_types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_logins_Username",
                table: "logins",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ot_assignments_StaffId",
                table: "ot_assignments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_LoginId",
                table: "password_reset_tokens",
                column: "LoginId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_Token",
                table: "password_reset_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pay_bill_entries_StaffId_Year_Month",
                table: "pay_bill_entries",
                columns: new[] { "StaffId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pay_bill_entries_Year_Month",
                table: "pay_bill_entries",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_Name",
                table: "product_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId",
                table: "products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductCode",
                table: "products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_StallCode",
                table: "products",
                column: "StallCode");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_ProductId",
                table: "sale_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_SalesEntryId",
                table: "sale_items",
                column: "SalesEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_StallCode",
                table: "sale_items",
                column: "StallCode");

            migrationBuilder.CreateIndex(
                name: "IX_sales_entries_EntryNo",
                table: "sales_entries",
                column: "EntryNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_entries_SoldAt",
                table: "sales_entries",
                column: "SoldAt");

            migrationBuilder.CreateIndex(
                name: "IX_Shift_Code",
                table: "Shift",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_AadhaarHash",
                table: "staff",
                column: "AadhaarHash",
                unique: true,
                filter: "[AadhaarHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_DepartmentId",
                table: "staff",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_EmployeeId",
                table: "staff",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_Mobile",
                table: "staff",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_entries_StaffId_FromDate",
                table: "staff_leave_entries",
                columns: new[] { "StaffId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_entries_Year",
                table: "staff_leave_entries",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_CurrentArtisanId",
                table: "stall_reallocations",
                column: "CurrentArtisanId");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_NewArtisanId",
                table: "stall_reallocations",
                column: "NewArtisanId");

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_RequestNo",
                table: "stall_reallocations",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stall_reallocations_StallId_Status",
                table: "stall_reallocations",
                columns: new[] { "StallId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stalls_ArtisanId",
                table: "stalls",
                column: "ArtisanId",
                unique: true,
                filter: "[ArtisanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_CategoryId",
                table: "stalls",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_StallCode",
                table: "stalls",
                column: "StallCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stalls_Status",
                table: "stalls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_CreatedAt",
                table: "stock_movements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId",
                table: "stock_movements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_ExpiresAt",
                table: "user_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_LoginId",
                table: "user_sessions",
                column: "LoginId");

            // ── the shifts offered on the staff form (were inserted by earlier migrations) ──
            migrationBuilder.Sql(@"
INSERT INTO [Shift] (Code, Name, DisplayOrder) VALUES
    (N'06:00 AM to 02:00 PM', N'06:00 AM to 02:00 PM', 1),
    (N'02:00 PM to 10:00 PM', N'02:00 PM to 10:00 PM', 2),
    (N'10:00 PM to 06:00 AM', N'10:00 PM to 06:00 AM', 3),
    (N'08:00 AM to 04:00 PM', N'08:00 AM to 04:00 PM', 4),
    (N'01:00 PM to 09:00 PM', N'01:00 PM to 09:00 PM', 5),
    (N'10:00 AM to 05:00 PM', N'10:00 AM to 05:00 PM', 6),
    (N'10:00 AM to 06:00 PM', N'10:00 AM to 06:00 PM', 7);");

            // ── the departments (Staff Category); no screen creates them, and the staff form needs one ──
            migrationBuilder.Sql(@"
INSERT INTO departments (Name) VALUES (N'Accounts'), (N'Admin'), (N'Housekeeping'), (N'Sales'), (N'Security');");

            // ── SERP.UpdateAttendanceManagement for int StaffId and an IDENTITY attendance_records.Id ──
            MigrationScript.Run(migrationBuilder, "Triggers/UpdateAttendanceManagement.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // going back would mean dropping every table again for Guid keys, so data could only come from a backup
            throw new NotSupportedException(
                "UseIntIdentityKeys cannot be reverted: the IMSB tables were re-created with int keys. Restore a database backup taken before it.");
        }
    }
}