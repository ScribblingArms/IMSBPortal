using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class RenameColumnsToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artisan_applications_artisans_artisan_id",
                table: "artisan_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_artisan_applications_product_categories_category_id",
                table: "artisan_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_artisans_product_categories_category_id",
                table: "artisans");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_staff_staff_id",
                table: "attendance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_leave_entitlements_staff_staff_id",
                table: "leave_entitlements");

            migrationBuilder.DropForeignKey(
                name: "FK_password_reset_tokens_logins_login_id",
                table: "password_reset_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_products_product_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_sales_entries_sales_entry_id",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "FK_staff_leave_entries_staff_staff_id",
                table: "staff_leave_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_artisans_current_artisan_id",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_artisans_new_artisan_id",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_stalls_stall_id",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stalls_artisans_artisan_id",
                table: "stalls");

            migrationBuilder.DropForeignKey(
                name: "FK_stalls_product_categories_category_id",
                table: "stalls");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_products_product_id",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_logins_login_id",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_stalls_artisan_id",
                table: "stalls");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_id_leave_type_year",
                table: "leave_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_staff_id_year",
                table: "leave_entitlements");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "user_sessions",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "user_sessions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_agent",
                table: "user_sessions",
                newName: "UserAgent");

            migrationBuilder.RenameColumn(
                name: "revoked_reason",
                table: "user_sessions",
                newName: "RevokedReason");

            migrationBuilder.RenameColumn(
                name: "revoked_at",
                table: "user_sessions",
                newName: "RevokedAt");

            migrationBuilder.RenameColumn(
                name: "login_id",
                table: "user_sessions",
                newName: "LoginId");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "user_sessions",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "is_persistent",
                table: "user_sessions",
                newName: "IsPersistent");

            migrationBuilder.RenameColumn(
                name: "ip_address",
                table: "user_sessions",
                newName: "IpAddress");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "user_sessions",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "user_sessions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_login_id",
                table: "user_sessions",
                newName: "IX_user_sessions_LoginId");

            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_expires_at",
                table: "user_sessions",
                newName: "IX_user_sessions_ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "stock_movements",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "stock_movements",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "stock_movements",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "quantity_before",
                table: "stock_movements",
                newName: "QuantityBefore");

            migrationBuilder.RenameColumn(
                name: "quantity_after",
                table: "stock_movements",
                newName: "QuantityAfter");

            migrationBuilder.RenameColumn(
                name: "product_name",
                table: "stock_movements",
                newName: "ProductName");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "stock_movements",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "product_code",
                table: "stock_movements",
                newName: "ProductCode");

            migrationBuilder.RenameColumn(
                name: "movement_type",
                table: "stock_movements",
                newName: "MovementType");

            migrationBuilder.RenameColumn(
                name: "movement_date",
                table: "stock_movements",
                newName: "MovementDate");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "stock_movements",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "stock_movements",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_stock_movements_product_id",
                table: "stock_movements",
                newName: "IX_stock_movements_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_stock_movements_created_at",
                table: "stock_movements",
                newName: "IX_stock_movements_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "stalls",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "stalls",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "stalls",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "location",
                table: "stalls",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "block",
                table: "stalls",
                newName: "Block");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "stalls",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "stalls",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "stalls",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "stall_code",
                table: "stalls",
                newName: "StallCode");

            migrationBuilder.RenameColumn(
                name: "row_label",
                table: "stalls",
                newName: "RowLabel");

            migrationBuilder.RenameColumn(
                name: "monthly_rent",
                table: "stalls",
                newName: "MonthlyRent");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "stalls",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "stalls",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "stalls",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "artisan_id",
                table: "stalls",
                newName: "ArtisanId");

            migrationBuilder.RenameColumn(
                name: "allotted_date",
                table: "stalls",
                newName: "AllottedDate");

            migrationBuilder.RenameColumn(
                name: "agreement_end_date",
                table: "stalls",
                newName: "AgreementEndDate");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_status",
                table: "stalls",
                newName: "IX_stalls_Status");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_stall_code",
                table: "stalls",
                newName: "IX_stalls_StallCode");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_category_id",
                table: "stalls",
                newName: "IX_stalls_CategoryId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "stall_reallocations",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "stall_reallocations",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "stall_reallocations",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "stall_reallocations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "stall_id",
                table: "stall_reallocations",
                newName: "StallId");

            migrationBuilder.RenameColumn(
                name: "stall_code",
                table: "stall_reallocations",
                newName: "StallCode");

            migrationBuilder.RenameColumn(
                name: "requested_date",
                table: "stall_reallocations",
                newName: "RequestedDate");

            migrationBuilder.RenameColumn(
                name: "request_no",
                table: "stall_reallocations",
                newName: "RequestNo");

            migrationBuilder.RenameColumn(
                name: "new_artisan_name",
                table: "stall_reallocations",
                newName: "NewArtisanName");

            migrationBuilder.RenameColumn(
                name: "new_artisan_id",
                table: "stall_reallocations",
                newName: "NewArtisanId");

            migrationBuilder.RenameColumn(
                name: "decision_remarks",
                table: "stall_reallocations",
                newName: "DecisionRemarks");

            migrationBuilder.RenameColumn(
                name: "decided_by",
                table: "stall_reallocations",
                newName: "DecidedBy");

            migrationBuilder.RenameColumn(
                name: "decided_at",
                table: "stall_reallocations",
                newName: "DecidedAt");

            migrationBuilder.RenameColumn(
                name: "current_artisan_name",
                table: "stall_reallocations",
                newName: "CurrentArtisanName");

            migrationBuilder.RenameColumn(
                name: "current_artisan_id",
                table: "stall_reallocations",
                newName: "CurrentArtisanId");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "stall_reallocations",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "stall_reallocations",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_stall_id_status",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_StallId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_request_no",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_RequestNo");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_new_artisan_id",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_NewArtisanId");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_current_artisan_id",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_CurrentArtisanId");

            migrationBuilder.RenameColumn(
                name: "year",
                table: "staff_leave_entries",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "staff_leave_entries",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "days",
                table: "staff_leave_entries",
                newName: "Days");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "staff_leave_entries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "staff_leave_entries",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "staff_leave_entries",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "to_date",
                table: "staff_leave_entries",
                newName: "ToDate");

            migrationBuilder.RenameColumn(
                name: "staff_id",
                table: "staff_leave_entries",
                newName: "StaffId");

            migrationBuilder.RenameColumn(
                name: "leave_type",
                table: "staff_leave_entries",
                newName: "LeaveType");

            migrationBuilder.RenameColumn(
                name: "from_date",
                table: "staff_leave_entries",
                newName: "FromDate");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "staff_leave_entries",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "staff_leave_entries",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_staff_leave_entries_year",
                table: "staff_leave_entries",
                newName: "IX_staff_leave_entries_Year");

            migrationBuilder.RenameIndex(
                name: "IX_staff_leave_entries_staff_id_from_date",
                table: "staff_leave_entries",
                newName: "IX_staff_leave_entries_StaffId_FromDate");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "staff",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "shift",
                table: "staff",
                newName: "Shift");

            migrationBuilder.RenameColumn(
                name: "mobile",
                table: "staff",
                newName: "Mobile");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "staff",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "staff",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "designation",
                table: "staff",
                newName: "Designation");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "staff",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "staff",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "staff",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "staff_category",
                table: "staff",
                newName: "StaffCategory");

            migrationBuilder.RenameColumn(
                name: "ot_to_time",
                table: "staff",
                newName: "OtToTime");

            migrationBuilder.RenameColumn(
                name: "ot_from_time",
                table: "staff",
                newName: "OtFromTime");

            migrationBuilder.RenameColumn(
                name: "monthly_salary",
                table: "staff",
                newName: "MonthlySalary");

            migrationBuilder.RenameColumn(
                name: "join_date",
                table: "staff",
                newName: "JoinDate");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "staff",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "employee_id",
                table: "staff",
                newName: "EmployeeId");

            migrationBuilder.RenameColumn(
                name: "department_id",
                table: "staff",
                newName: "DepartmentId");

            migrationBuilder.RenameColumn(
                name: "date_of_birth",
                table: "staff",
                newName: "DateOfBirth");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "staff",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "staff",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_staff_mobile",
                table: "staff",
                newName: "IX_staff_Mobile");

            migrationBuilder.RenameIndex(
                name: "IX_staff_employee_id",
                table: "staff",
                newName: "IX_staff_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_staff_department_id",
                table: "staff",
                newName: "IX_staff_DepartmentId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Shift",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "sales_entries",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sales_entries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "sales_entries",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "sales_entries",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "total_quantity",
                table: "sales_entries",
                newName: "TotalQuantity");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "sales_entries",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "sold_at",
                table: "sales_entries",
                newName: "SoldAt");

            migrationBuilder.RenameColumn(
                name: "entry_no",
                table: "sales_entries",
                newName: "EntryNo");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "sales_entries",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "sales_entries",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_sales_entries_sold_at",
                table: "sales_entries",
                newName: "IX_sales_entries_SoldAt");

            migrationBuilder.RenameIndex(
                name: "IX_sales_entries_entry_no",
                table: "sales_entries",
                newName: "IX_sales_entries_EntryNo");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "sale_items",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sale_items",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "sale_items",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "stall_code",
                table: "sale_items",
                newName: "StallCode");

            migrationBuilder.RenameColumn(
                name: "sales_entry_id",
                table: "sale_items",
                newName: "SalesEntryId");

            migrationBuilder.RenameColumn(
                name: "product_name",
                table: "sale_items",
                newName: "ProductName");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "sale_items",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "product_code",
                table: "sale_items",
                newName: "ProductCode");

            migrationBuilder.RenameColumn(
                name: "line_total",
                table: "sale_items",
                newName: "LineTotal");

            migrationBuilder.RenameColumn(
                name: "category_name",
                table: "sale_items",
                newName: "CategoryName");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "sale_items",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_stall_code",
                table: "sale_items",
                newName: "IX_sale_items_StallCode");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_sales_entry_id",
                table: "sale_items",
                newName: "IX_sale_items_SalesEntryId");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_product_id",
                table: "sale_items",
                newName: "IX_sale_items_ProductId");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "products",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "products",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "products",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "products",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "products",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "products",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "stall_code",
                table: "products",
                newName: "StallCode");

            migrationBuilder.RenameColumn(
                name: "product_code",
                table: "products",
                newName: "ProductCode");

            migrationBuilder.RenameColumn(
                name: "min_stock",
                table: "products",
                newName: "MinStock");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "products",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "products",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "products",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_products_stall_code",
                table: "products",
                newName: "IX_products_StallCode");

            migrationBuilder.RenameIndex(
                name: "IX_products_product_code",
                table: "products",
                newName: "IX_products_ProductCode");

            migrationBuilder.RenameIndex(
                name: "IX_products_category_id",
                table: "products",
                newName: "IX_products_CategoryId");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "product_categories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "product_categories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "sort_order",
                table: "product_categories",
                newName: "SortOrder");

            migrationBuilder.RenameIndex(
                name: "IX_product_categories_name",
                table: "product_categories",
                newName: "IX_product_categories_Name");

            migrationBuilder.RenameColumn(
                name: "year",
                table: "pay_bill_entries",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "month",
                table: "pay_bill_entries",
                newName: "Month");

            migrationBuilder.RenameColumn(
                name: "designation",
                table: "pay_bill_entries",
                newName: "Designation");

            migrationBuilder.RenameColumn(
                name: "department",
                table: "pay_bill_entries",
                newName: "Department");

            migrationBuilder.RenameColumn(
                name: "deductions",
                table: "pay_bill_entries",
                newName: "Deductions");

            migrationBuilder.RenameColumn(
                name: "allowances",
                table: "pay_bill_entries",
                newName: "Allowances");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "pay_bill_entries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "staff_id",
                table: "pay_bill_entries",
                newName: "StaffId");

            migrationBuilder.RenameColumn(
                name: "paid_days",
                table: "pay_bill_entries",
                newName: "PaidDays");

            migrationBuilder.RenameColumn(
                name: "overtime_hours",
                table: "pay_bill_entries",
                newName: "OvertimeHours");

            migrationBuilder.RenameColumn(
                name: "overtime_amount",
                table: "pay_bill_entries",
                newName: "OvertimeAmount");

            migrationBuilder.RenameColumn(
                name: "net_pay",
                table: "pay_bill_entries",
                newName: "NetPay");

            migrationBuilder.RenameColumn(
                name: "monthly_salary",
                table: "pay_bill_entries",
                newName: "MonthlySalary");

            migrationBuilder.RenameColumn(
                name: "month_days",
                table: "pay_bill_entries",
                newName: "MonthDays");

            migrationBuilder.RenameColumn(
                name: "generated_at",
                table: "pay_bill_entries",
                newName: "GeneratedAt");

            migrationBuilder.RenameColumn(
                name: "employee_name",
                table: "pay_bill_entries",
                newName: "EmployeeName");

            migrationBuilder.RenameColumn(
                name: "employee_id",
                table: "pay_bill_entries",
                newName: "EmployeeId");

            migrationBuilder.RenameColumn(
                name: "basic_pay",
                table: "pay_bill_entries",
                newName: "BasicPay");

            migrationBuilder.RenameIndex(
                name: "IX_pay_bill_entries_year_month",
                table: "pay_bill_entries",
                newName: "IX_pay_bill_entries_Year_Month");

            migrationBuilder.RenameIndex(
                name: "IX_pay_bill_entries_staff_id_year_month",
                table: "pay_bill_entries",
                newName: "IX_pay_bill_entries_StaffId_Year_Month");

            migrationBuilder.RenameColumn(
                name: "token",
                table: "password_reset_tokens",
                newName: "Token");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "password_reset_tokens",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "login_id",
                table: "password_reset_tokens",
                newName: "LoginId");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "password_reset_tokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "password_reset_tokens",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_token",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_Token");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_login_id",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_LoginId");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "logins",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "logins",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "logins",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "logins",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "logins",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "last_login",
                table: "logins",
                newName: "LastLogin");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "logins",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "logins",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_logins_username",
                table: "logins",
                newName: "IX_logins_Username");

            migrationBuilder.RenameColumn(
                name: "year",
                table: "leave_entitlements",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "leave_entitlements",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "staff_id",
                table: "leave_entitlements",
                newName: "StaffId");

            migrationBuilder.RenameColumn(
                name: "staff_category",
                table: "leave_entitlements",
                newName: "StaffCategory");

            migrationBuilder.RenameColumn(
                name: "leave_type",
                table: "leave_entitlements",
                newName: "LeaveType");

            migrationBuilder.RenameColumn(
                name: "attendance_leave_type",
                table: "leave_entitlements",
                newName: "AttendanceLeaveType");

            migrationBuilder.RenameColumn(
                name: "annual_quota",
                table: "leave_entitlements",
                newName: "AnnualQuota");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "departments",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "departments",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_departments_name",
                table: "departments",
                newName: "IX_departments_Name");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "attendance_records",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "attendance_records",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "attendance_records",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "worked_hours",
                table: "attendance_records",
                newName: "WorkedHours");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "attendance_records",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "attendance_records",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "staff_id",
                table: "attendance_records",
                newName: "StaffId");

            migrationBuilder.RenameColumn(
                name: "overtime_hours",
                table: "attendance_records",
                newName: "OvertimeHours");

            migrationBuilder.RenameColumn(
                name: "out_time",
                table: "attendance_records",
                newName: "OutTime");

            migrationBuilder.RenameColumn(
                name: "leave_without_pay",
                table: "attendance_records",
                newName: "LeaveWithoutPay");

            migrationBuilder.RenameColumn(
                name: "in_time",
                table: "attendance_records",
                newName: "InTime");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "attendance_records",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "attendance_records",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "attendance_date",
                table: "attendance_records",
                newName: "AttendanceDate");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_status",
                table: "attendance_records",
                newName: "IX_attendance_records_Status");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_staff_id_attendance_date",
                table: "attendance_records",
                newName: "IX_attendance_records_StaffId_AttendanceDate");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_attendance_date",
                table: "attendance_records",
                newName: "IX_attendance_records_AttendanceDate");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "artisans",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "skills",
                table: "artisans",
                newName: "Skills");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "artisans",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "mobile",
                table: "artisans",
                newName: "Mobile");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "artisans",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "artisans",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "district",
                table: "artisans",
                newName: "District");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "artisans",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "artisans",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "years_of_experience",
                table: "artisans",
                newName: "YearsOfExperience");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "artisans",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "artisans",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "to_date",
                table: "artisans",
                newName: "ToDate");

            migrationBuilder.RenameColumn(
                name: "joined_date",
                table: "artisans",
                newName: "JoinedDate");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "artisans",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "from_date",
                table: "artisans",
                newName: "FromDate");

            migrationBuilder.RenameColumn(
                name: "date_of_birth",
                table: "artisans",
                newName: "DateOfBirth");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "artisans",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "artisans",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "artisans",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "artisan_code",
                table: "artisans",
                newName: "ArtisanCode");

            migrationBuilder.RenameColumn(
                name: "aadhaar_last4",
                table: "artisans",
                newName: "AadhaarLast4");

            migrationBuilder.RenameColumn(
                name: "aadhaar_hash",
                table: "artisans",
                newName: "AadhaarHash");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_status",
                table: "artisans",
                newName: "IX_artisans_Status");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_mobile",
                table: "artisans",
                newName: "IX_artisans_Mobile");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_category_id",
                table: "artisans",
                newName: "IX_artisans_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_artisan_code",
                table: "artisans",
                newName: "IX_artisans_ArtisanCode");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_aadhaar_hash",
                table: "artisans",
                newName: "IX_artisans_AadhaarHash");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "artisan_applications",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "skills",
                table: "artisan_applications",
                newName: "Skills");

            migrationBuilder.RenameColumn(
                name: "reviewer",
                table: "artisan_applications",
                newName: "Reviewer");

            migrationBuilder.RenameColumn(
                name: "remarks",
                table: "artisan_applications",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "mobile",
                table: "artisan_applications",
                newName: "Mobile");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "artisan_applications",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "artisan_applications",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "district",
                table: "artisan_applications",
                newName: "District");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "artisan_applications",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "artisan_applications",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "years_of_experience",
                table: "artisan_applications",
                newName: "YearsOfExperience");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "artisan_applications",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "artisan_applications",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "to_date",
                table: "artisan_applications",
                newName: "ToDate");

            migrationBuilder.RenameColumn(
                name: "submitted_date",
                table: "artisan_applications",
                newName: "SubmittedDate");

            migrationBuilder.RenameColumn(
                name: "stall_code",
                table: "artisan_applications",
                newName: "StallCode");

            migrationBuilder.RenameColumn(
                name: "review_started_at",
                table: "artisan_applications",
                newName: "ReviewStartedAt");

            migrationBuilder.RenameColumn(
                name: "full_name",
                table: "artisan_applications",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "from_date",
                table: "artisan_applications",
                newName: "FromDate");

            migrationBuilder.RenameColumn(
                name: "decision_remarks",
                table: "artisan_applications",
                newName: "DecisionRemarks");

            migrationBuilder.RenameColumn(
                name: "decided_by",
                table: "artisan_applications",
                newName: "DecidedBy");

            migrationBuilder.RenameColumn(
                name: "decided_at",
                table: "artisan_applications",
                newName: "DecidedAt");

            migrationBuilder.RenameColumn(
                name: "date_of_birth",
                table: "artisan_applications",
                newName: "DateOfBirth");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "artisan_applications",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "artisan_applications",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "artisan_applications",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "artisan_id",
                table: "artisan_applications",
                newName: "ArtisanId");

            migrationBuilder.RenameColumn(
                name: "artisan_code",
                table: "artisan_applications",
                newName: "ArtisanCode");

            migrationBuilder.RenameColumn(
                name: "application_no",
                table: "artisan_applications",
                newName: "ApplicationNo");

            migrationBuilder.RenameColumn(
                name: "aadhaar_last4",
                table: "artisan_applications",
                newName: "AadhaarLast4");

            migrationBuilder.RenameColumn(
                name: "aadhaar_hash",
                table: "artisan_applications",
                newName: "AadhaarHash");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_status",
                table: "artisan_applications",
                newName: "IX_artisan_applications_Status");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_mobile",
                table: "artisan_applications",
                newName: "IX_artisan_applications_Mobile");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_submitted_date",
                table: "artisan_applications",
                newName: "IX_artisan_applications_SubmittedDate");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_category_id",
                table: "artisan_applications",
                newName: "IX_artisan_applications_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_artisan_id",
                table: "artisan_applications",
                newName: "IX_artisan_applications_ArtisanId");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_application_no",
                table: "artisan_applications",
                newName: "IX_artisan_applications_ApplicationNo");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_aadhaar_hash",
                table: "artisan_applications",
                newName: "IX_artisan_applications_AadhaarHash");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_ArtisanId",
                table: "stalls",
                column: "ArtisanId",
                unique: true,
                filter: "[ArtisanId] IS NOT NULL");

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

            migrationBuilder.AddForeignKey(
                name: "FK_artisan_applications_artisans_ArtisanId",
                table: "artisan_applications",
                column: "ArtisanId",
                principalTable: "artisans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_artisan_applications_product_categories_CategoryId",
                table: "artisan_applications",
                column: "CategoryId",
                principalTable: "product_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_artisans_product_categories_CategoryId",
                table: "artisans",
                column: "CategoryId",
                principalTable: "product_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_staff_StaffId",
                table: "attendance_records",
                column: "StaffId",
                principalTable: "staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_leave_entitlements_staff_StaffId",
                table: "leave_entitlements",
                column: "StaffId",
                principalTable: "staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_password_reset_tokens_logins_LoginId",
                table: "password_reset_tokens",
                column: "LoginId",
                principalTable: "logins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_categories_CategoryId",
                table: "products",
                column: "CategoryId",
                principalTable: "product_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_products_ProductId",
                table: "sale_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_sales_entries_SalesEntryId",
                table: "sale_items",
                column: "SalesEntryId",
                principalTable: "sales_entries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_leave_entries_staff_StaffId",
                table: "staff_leave_entries",
                column: "StaffId",
                principalTable: "staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_artisans_CurrentArtisanId",
                table: "stall_reallocations",
                column: "CurrentArtisanId",
                principalTable: "artisans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_artisans_NewArtisanId",
                table: "stall_reallocations",
                column: "NewArtisanId",
                principalTable: "artisans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_stalls_StallId",
                table: "stall_reallocations",
                column: "StallId",
                principalTable: "stalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stalls_artisans_ArtisanId",
                table: "stalls",
                column: "ArtisanId",
                principalTable: "artisans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stalls_product_categories_CategoryId",
                table: "stalls",
                column: "CategoryId",
                principalTable: "product_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_products_ProductId",
                table: "stock_movements",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_logins_LoginId",
                table: "user_sessions",
                column: "LoginId",
                principalTable: "logins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artisan_applications_artisans_ArtisanId",
                table: "artisan_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_artisan_applications_product_categories_CategoryId",
                table: "artisan_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_artisans_product_categories_CategoryId",
                table: "artisans");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_records_staff_StaffId",
                table: "attendance_records");

            migrationBuilder.DropForeignKey(
                name: "FK_leave_entitlements_staff_StaffId",
                table: "leave_entitlements");

            migrationBuilder.DropForeignKey(
                name: "FK_password_reset_tokens_logins_LoginId",
                table: "password_reset_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_categories_CategoryId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_products_ProductId",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_sales_entries_SalesEntryId",
                table: "sale_items");

            migrationBuilder.DropForeignKey(
                name: "FK_staff_leave_entries_staff_StaffId",
                table: "staff_leave_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_artisans_CurrentArtisanId",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_artisans_NewArtisanId",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stall_reallocations_stalls_StallId",
                table: "stall_reallocations");

            migrationBuilder.DropForeignKey(
                name: "FK_stalls_artisans_ArtisanId",
                table: "stalls");

            migrationBuilder.DropForeignKey(
                name: "FK_stalls_product_categories_CategoryId",
                table: "stalls");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_products_ProductId",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_logins_LoginId",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_stalls_ArtisanId",
                table: "stalls");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_StaffId_LeaveType_Year",
                table: "leave_entitlements");

            migrationBuilder.DropIndex(
                name: "IX_leave_entitlements_StaffId_Year",
                table: "leave_entitlements");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "user_sessions",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "user_sessions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserAgent",
                table: "user_sessions",
                newName: "user_agent");

            migrationBuilder.RenameColumn(
                name: "RevokedReason",
                table: "user_sessions",
                newName: "revoked_reason");

            migrationBuilder.RenameColumn(
                name: "RevokedAt",
                table: "user_sessions",
                newName: "revoked_at");

            migrationBuilder.RenameColumn(
                name: "LoginId",
                table: "user_sessions",
                newName: "login_id");

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "user_sessions",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "IsPersistent",
                table: "user_sessions",
                newName: "is_persistent");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "user_sessions",
                newName: "ip_address");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "user_sessions",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "user_sessions",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_LoginId",
                table: "user_sessions",
                newName: "IX_user_sessions_login_id");

            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_ExpiresAt",
                table: "user_sessions",
                newName: "IX_user_sessions_expires_at");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "stock_movements",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "stock_movements",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "stock_movements",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "QuantityBefore",
                table: "stock_movements",
                newName: "quantity_before");

            migrationBuilder.RenameColumn(
                name: "QuantityAfter",
                table: "stock_movements",
                newName: "quantity_after");

            migrationBuilder.RenameColumn(
                name: "ProductName",
                table: "stock_movements",
                newName: "product_name");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "stock_movements",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "ProductCode",
                table: "stock_movements",
                newName: "product_code");

            migrationBuilder.RenameColumn(
                name: "MovementType",
                table: "stock_movements",
                newName: "movement_type");

            migrationBuilder.RenameColumn(
                name: "MovementDate",
                table: "stock_movements",
                newName: "movement_date");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "stock_movements",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "stock_movements",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_stock_movements_ProductId",
                table: "stock_movements",
                newName: "IX_stock_movements_product_id");

            migrationBuilder.RenameIndex(
                name: "IX_stock_movements_CreatedAt",
                table: "stock_movements",
                newName: "IX_stock_movements_created_at");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "stalls",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "stalls",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "stalls",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "stalls",
                newName: "location");

            migrationBuilder.RenameColumn(
                name: "Block",
                table: "stalls",
                newName: "block");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "stalls",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "stalls",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "stalls",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "StallCode",
                table: "stalls",
                newName: "stall_code");

            migrationBuilder.RenameColumn(
                name: "RowLabel",
                table: "stalls",
                newName: "row_label");

            migrationBuilder.RenameColumn(
                name: "MonthlyRent",
                table: "stalls",
                newName: "monthly_rent");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "stalls",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "stalls",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "stalls",
                newName: "category_id");

            migrationBuilder.RenameColumn(
                name: "ArtisanId",
                table: "stalls",
                newName: "artisan_id");

            migrationBuilder.RenameColumn(
                name: "AllottedDate",
                table: "stalls",
                newName: "allotted_date");

            migrationBuilder.RenameColumn(
                name: "AgreementEndDate",
                table: "stalls",
                newName: "agreement_end_date");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_Status",
                table: "stalls",
                newName: "IX_stalls_status");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_StallCode",
                table: "stalls",
                newName: "IX_stalls_stall_code");

            migrationBuilder.RenameIndex(
                name: "IX_stalls_CategoryId",
                table: "stalls",
                newName: "IX_stalls_category_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "stall_reallocations",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "stall_reallocations",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "stall_reallocations",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "stall_reallocations",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "StallId",
                table: "stall_reallocations",
                newName: "stall_id");

            migrationBuilder.RenameColumn(
                name: "StallCode",
                table: "stall_reallocations",
                newName: "stall_code");

            migrationBuilder.RenameColumn(
                name: "RequestedDate",
                table: "stall_reallocations",
                newName: "requested_date");

            migrationBuilder.RenameColumn(
                name: "RequestNo",
                table: "stall_reallocations",
                newName: "request_no");

            migrationBuilder.RenameColumn(
                name: "NewArtisanName",
                table: "stall_reallocations",
                newName: "new_artisan_name");

            migrationBuilder.RenameColumn(
                name: "NewArtisanId",
                table: "stall_reallocations",
                newName: "new_artisan_id");

            migrationBuilder.RenameColumn(
                name: "DecisionRemarks",
                table: "stall_reallocations",
                newName: "decision_remarks");

            migrationBuilder.RenameColumn(
                name: "DecidedBy",
                table: "stall_reallocations",
                newName: "decided_by");

            migrationBuilder.RenameColumn(
                name: "DecidedAt",
                table: "stall_reallocations",
                newName: "decided_at");

            migrationBuilder.RenameColumn(
                name: "CurrentArtisanName",
                table: "stall_reallocations",
                newName: "current_artisan_name");

            migrationBuilder.RenameColumn(
                name: "CurrentArtisanId",
                table: "stall_reallocations",
                newName: "current_artisan_id");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "stall_reallocations",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "stall_reallocations",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_StallId_Status",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_stall_id_status");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_RequestNo",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_request_no");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_NewArtisanId",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_new_artisan_id");

            migrationBuilder.RenameIndex(
                name: "IX_stall_reallocations_CurrentArtisanId",
                table: "stall_reallocations",
                newName: "IX_stall_reallocations_current_artisan_id");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "staff_leave_entries",
                newName: "year");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "staff_leave_entries",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Days",
                table: "staff_leave_entries",
                newName: "days");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "staff_leave_entries",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "staff_leave_entries",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "staff_leave_entries",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ToDate",
                table: "staff_leave_entries",
                newName: "to_date");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "staff_leave_entries",
                newName: "staff_id");

            migrationBuilder.RenameColumn(
                name: "LeaveType",
                table: "staff_leave_entries",
                newName: "leave_type");

            migrationBuilder.RenameColumn(
                name: "FromDate",
                table: "staff_leave_entries",
                newName: "from_date");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "staff_leave_entries",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "staff_leave_entries",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_staff_leave_entries_Year",
                table: "staff_leave_entries",
                newName: "IX_staff_leave_entries_year");

            migrationBuilder.RenameIndex(
                name: "IX_staff_leave_entries_StaffId_FromDate",
                table: "staff_leave_entries",
                newName: "IX_staff_leave_entries_staff_id_from_date");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "staff",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Shift",
                table: "staff",
                newName: "shift");

            migrationBuilder.RenameColumn(
                name: "Mobile",
                table: "staff",
                newName: "mobile");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "staff",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "staff",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Designation",
                table: "staff",
                newName: "designation");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "staff",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "staff",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "staff",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "StaffCategory",
                table: "staff",
                newName: "staff_category");

            migrationBuilder.RenameColumn(
                name: "OtToTime",
                table: "staff",
                newName: "ot_to_time");

            migrationBuilder.RenameColumn(
                name: "OtFromTime",
                table: "staff",
                newName: "ot_from_time");

            migrationBuilder.RenameColumn(
                name: "MonthlySalary",
                table: "staff",
                newName: "monthly_salary");

            migrationBuilder.RenameColumn(
                name: "JoinDate",
                table: "staff",
                newName: "join_date");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "staff",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "staff",
                newName: "employee_id");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "staff",
                newName: "department_id");

            migrationBuilder.RenameColumn(
                name: "DateOfBirth",
                table: "staff",
                newName: "date_of_birth");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "staff",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "staff",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_staff_Mobile",
                table: "staff",
                newName: "IX_staff_mobile");

            migrationBuilder.RenameIndex(
                name: "IX_staff_EmployeeId",
                table: "staff",
                newName: "IX_staff_employee_id");

            migrationBuilder.RenameIndex(
                name: "IX_staff_DepartmentId",
                table: "staff",
                newName: "IX_staff_department_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Shift",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "sales_entries",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sales_entries",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "sales_entries",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "sales_entries",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TotalQuantity",
                table: "sales_entries",
                newName: "total_quantity");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "sales_entries",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "SoldAt",
                table: "sales_entries",
                newName: "sold_at");

            migrationBuilder.RenameColumn(
                name: "EntryNo",
                table: "sales_entries",
                newName: "entry_no");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "sales_entries",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "sales_entries",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_sales_entries_SoldAt",
                table: "sales_entries",
                newName: "IX_sales_entries_sold_at");

            migrationBuilder.RenameIndex(
                name: "IX_sales_entries_EntryNo",
                table: "sales_entries",
                newName: "IX_sales_entries_entry_no");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "sale_items",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sale_items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "sale_items",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "StallCode",
                table: "sale_items",
                newName: "stall_code");

            migrationBuilder.RenameColumn(
                name: "SalesEntryId",
                table: "sale_items",
                newName: "sales_entry_id");

            migrationBuilder.RenameColumn(
                name: "ProductName",
                table: "sale_items",
                newName: "product_name");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "sale_items",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "ProductCode",
                table: "sale_items",
                newName: "product_code");

            migrationBuilder.RenameColumn(
                name: "LineTotal",
                table: "sale_items",
                newName: "line_total");

            migrationBuilder.RenameColumn(
                name: "CategoryName",
                table: "sale_items",
                newName: "category_name");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "sale_items",
                newName: "category_id");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_StallCode",
                table: "sale_items",
                newName: "IX_sale_items_stall_code");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_SalesEntryId",
                table: "sale_items",
                newName: "IX_sale_items_sales_entry_id");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_ProductId",
                table: "sale_items",
                newName: "IX_sale_items_product_id");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "products",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "products",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "products",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "products",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "products",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "products",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "StallCode",
                table: "products",
                newName: "stall_code");

            migrationBuilder.RenameColumn(
                name: "ProductCode",
                table: "products",
                newName: "product_code");

            migrationBuilder.RenameColumn(
                name: "MinStock",
                table: "products",
                newName: "min_stock");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "products",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "products",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "products",
                newName: "category_id");

            migrationBuilder.RenameIndex(
                name: "IX_products_StallCode",
                table: "products",
                newName: "IX_products_stall_code");

            migrationBuilder.RenameIndex(
                name: "IX_products_ProductCode",
                table: "products",
                newName: "IX_products_product_code");

            migrationBuilder.RenameIndex(
                name: "IX_products_CategoryId",
                table: "products",
                newName: "IX_products_category_id");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "product_categories",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "product_categories",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "product_categories",
                newName: "sort_order");

            migrationBuilder.RenameIndex(
                name: "IX_product_categories_Name",
                table: "product_categories",
                newName: "IX_product_categories_name");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "pay_bill_entries",
                newName: "year");

            migrationBuilder.RenameColumn(
                name: "Month",
                table: "pay_bill_entries",
                newName: "month");

            migrationBuilder.RenameColumn(
                name: "Designation",
                table: "pay_bill_entries",
                newName: "designation");

            migrationBuilder.RenameColumn(
                name: "Department",
                table: "pay_bill_entries",
                newName: "department");

            migrationBuilder.RenameColumn(
                name: "Deductions",
                table: "pay_bill_entries",
                newName: "deductions");

            migrationBuilder.RenameColumn(
                name: "Allowances",
                table: "pay_bill_entries",
                newName: "allowances");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "pay_bill_entries",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "pay_bill_entries",
                newName: "staff_id");

            migrationBuilder.RenameColumn(
                name: "PaidDays",
                table: "pay_bill_entries",
                newName: "paid_days");

            migrationBuilder.RenameColumn(
                name: "OvertimeHours",
                table: "pay_bill_entries",
                newName: "overtime_hours");

            migrationBuilder.RenameColumn(
                name: "OvertimeAmount",
                table: "pay_bill_entries",
                newName: "overtime_amount");

            migrationBuilder.RenameColumn(
                name: "NetPay",
                table: "pay_bill_entries",
                newName: "net_pay");

            migrationBuilder.RenameColumn(
                name: "MonthlySalary",
                table: "pay_bill_entries",
                newName: "monthly_salary");

            migrationBuilder.RenameColumn(
                name: "MonthDays",
                table: "pay_bill_entries",
                newName: "month_days");

            migrationBuilder.RenameColumn(
                name: "GeneratedAt",
                table: "pay_bill_entries",
                newName: "generated_at");

            migrationBuilder.RenameColumn(
                name: "EmployeeName",
                table: "pay_bill_entries",
                newName: "employee_name");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "pay_bill_entries",
                newName: "employee_id");

            migrationBuilder.RenameColumn(
                name: "BasicPay",
                table: "pay_bill_entries",
                newName: "basic_pay");

            migrationBuilder.RenameIndex(
                name: "IX_pay_bill_entries_Year_Month",
                table: "pay_bill_entries",
                newName: "IX_pay_bill_entries_year_month");

            migrationBuilder.RenameIndex(
                name: "IX_pay_bill_entries_StaffId_Year_Month",
                table: "pay_bill_entries",
                newName: "IX_pay_bill_entries_staff_id_year_month");

            migrationBuilder.RenameColumn(
                name: "Token",
                table: "password_reset_tokens",
                newName: "token");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "password_reset_tokens",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "LoginId",
                table: "password_reset_tokens",
                newName: "login_id");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "password_reset_tokens",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "password_reset_tokens",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_Token",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_token");

            migrationBuilder.RenameIndex(
                name: "IX_password_reset_tokens_LoginId",
                table: "password_reset_tokens",
                newName: "IX_password_reset_tokens_login_id");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "logins",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "logins",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "logins",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "logins",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "logins",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "LastLogin",
                table: "logins",
                newName: "last_login");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "logins",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "logins",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_logins_Username",
                table: "logins",
                newName: "IX_logins_username");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "leave_entitlements",
                newName: "year");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "leave_entitlements",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "leave_entitlements",
                newName: "staff_id");

            migrationBuilder.RenameColumn(
                name: "StaffCategory",
                table: "leave_entitlements",
                newName: "staff_category");

            migrationBuilder.RenameColumn(
                name: "LeaveType",
                table: "leave_entitlements",
                newName: "leave_type");

            migrationBuilder.RenameColumn(
                name: "AttendanceLeaveType",
                table: "leave_entitlements",
                newName: "attendance_leave_type");

            migrationBuilder.RenameColumn(
                name: "AnnualQuota",
                table: "leave_entitlements",
                newName: "annual_quota");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "departments",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "departments",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_departments_Name",
                table: "departments",
                newName: "IX_departments_name");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "attendance_records",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "attendance_records",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "attendance_records",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkedHours",
                table: "attendance_records",
                newName: "worked_hours");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "attendance_records",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "attendance_records",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "attendance_records",
                newName: "staff_id");

            migrationBuilder.RenameColumn(
                name: "OvertimeHours",
                table: "attendance_records",
                newName: "overtime_hours");

            migrationBuilder.RenameColumn(
                name: "OutTime",
                table: "attendance_records",
                newName: "out_time");

            migrationBuilder.RenameColumn(
                name: "LeaveWithoutPay",
                table: "attendance_records",
                newName: "leave_without_pay");

            migrationBuilder.RenameColumn(
                name: "InTime",
                table: "attendance_records",
                newName: "in_time");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "attendance_records",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "attendance_records",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "AttendanceDate",
                table: "attendance_records",
                newName: "attendance_date");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_Status",
                table: "attendance_records",
                newName: "IX_attendance_records_status");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_StaffId_AttendanceDate",
                table: "attendance_records",
                newName: "IX_attendance_records_staff_id_attendance_date");

            migrationBuilder.RenameIndex(
                name: "IX_attendance_records_AttendanceDate",
                table: "attendance_records",
                newName: "IX_attendance_records_attendance_date");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "artisans",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Skills",
                table: "artisans",
                newName: "skills");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "artisans",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Mobile",
                table: "artisans",
                newName: "mobile");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "artisans",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "artisans",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "District",
                table: "artisans",
                newName: "district");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "artisans",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "artisans",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "YearsOfExperience",
                table: "artisans",
                newName: "years_of_experience");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "artisans",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "artisans",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ToDate",
                table: "artisans",
                newName: "to_date");

            migrationBuilder.RenameColumn(
                name: "JoinedDate",
                table: "artisans",
                newName: "joined_date");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "artisans",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "FromDate",
                table: "artisans",
                newName: "from_date");

            migrationBuilder.RenameColumn(
                name: "DateOfBirth",
                table: "artisans",
                newName: "date_of_birth");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "artisans",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "artisans",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "artisans",
                newName: "category_id");

            migrationBuilder.RenameColumn(
                name: "ArtisanCode",
                table: "artisans",
                newName: "artisan_code");

            migrationBuilder.RenameColumn(
                name: "AadhaarLast4",
                table: "artisans",
                newName: "aadhaar_last4");

            migrationBuilder.RenameColumn(
                name: "AadhaarHash",
                table: "artisans",
                newName: "aadhaar_hash");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_Status",
                table: "artisans",
                newName: "IX_artisans_status");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_Mobile",
                table: "artisans",
                newName: "IX_artisans_mobile");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_CategoryId",
                table: "artisans",
                newName: "IX_artisans_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_ArtisanCode",
                table: "artisans",
                newName: "IX_artisans_artisan_code");

            migrationBuilder.RenameIndex(
                name: "IX_artisans_AadhaarHash",
                table: "artisans",
                newName: "IX_artisans_aadhaar_hash");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "artisan_applications",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Skills",
                table: "artisan_applications",
                newName: "skills");

            migrationBuilder.RenameColumn(
                name: "Reviewer",
                table: "artisan_applications",
                newName: "reviewer");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "artisan_applications",
                newName: "remarks");

            migrationBuilder.RenameColumn(
                name: "Mobile",
                table: "artisan_applications",
                newName: "mobile");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "artisan_applications",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "artisan_applications",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "District",
                table: "artisan_applications",
                newName: "district");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "artisan_applications",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "artisan_applications",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "YearsOfExperience",
                table: "artisan_applications",
                newName: "years_of_experience");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "artisan_applications",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "artisan_applications",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ToDate",
                table: "artisan_applications",
                newName: "to_date");

            migrationBuilder.RenameColumn(
                name: "SubmittedDate",
                table: "artisan_applications",
                newName: "submitted_date");

            migrationBuilder.RenameColumn(
                name: "StallCode",
                table: "artisan_applications",
                newName: "stall_code");

            migrationBuilder.RenameColumn(
                name: "ReviewStartedAt",
                table: "artisan_applications",
                newName: "review_started_at");

            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "artisan_applications",
                newName: "full_name");

            migrationBuilder.RenameColumn(
                name: "FromDate",
                table: "artisan_applications",
                newName: "from_date");

            migrationBuilder.RenameColumn(
                name: "DecisionRemarks",
                table: "artisan_applications",
                newName: "decision_remarks");

            migrationBuilder.RenameColumn(
                name: "DecidedBy",
                table: "artisan_applications",
                newName: "decided_by");

            migrationBuilder.RenameColumn(
                name: "DecidedAt",
                table: "artisan_applications",
                newName: "decided_at");

            migrationBuilder.RenameColumn(
                name: "DateOfBirth",
                table: "artisan_applications",
                newName: "date_of_birth");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "artisan_applications",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "artisan_applications",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "artisan_applications",
                newName: "category_id");

            migrationBuilder.RenameColumn(
                name: "ArtisanId",
                table: "artisan_applications",
                newName: "artisan_id");

            migrationBuilder.RenameColumn(
                name: "ArtisanCode",
                table: "artisan_applications",
                newName: "artisan_code");

            migrationBuilder.RenameColumn(
                name: "ApplicationNo",
                table: "artisan_applications",
                newName: "application_no");

            migrationBuilder.RenameColumn(
                name: "AadhaarLast4",
                table: "artisan_applications",
                newName: "aadhaar_last4");

            migrationBuilder.RenameColumn(
                name: "AadhaarHash",
                table: "artisan_applications",
                newName: "aadhaar_hash");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_Status",
                table: "artisan_applications",
                newName: "IX_artisan_applications_status");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_Mobile",
                table: "artisan_applications",
                newName: "IX_artisan_applications_mobile");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_SubmittedDate",
                table: "artisan_applications",
                newName: "IX_artisan_applications_submitted_date");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_CategoryId",
                table: "artisan_applications",
                newName: "IX_artisan_applications_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_ArtisanId",
                table: "artisan_applications",
                newName: "IX_artisan_applications_artisan_id");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_ApplicationNo",
                table: "artisan_applications",
                newName: "IX_artisan_applications_application_no");

            migrationBuilder.RenameIndex(
                name: "IX_artisan_applications_AadhaarHash",
                table: "artisan_applications",
                newName: "IX_artisan_applications_aadhaar_hash");

            migrationBuilder.CreateIndex(
                name: "IX_stalls_artisan_id",
                table: "stalls",
                column: "artisan_id",
                unique: true,
                filter: "[artisan_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_id_leave_type_year",
                table: "leave_entitlements",
                columns: new[] { "staff_id", "leave_type", "year" },
                unique: true,
                filter: "[staff_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_entitlements_staff_id_year",
                table: "leave_entitlements",
                columns: new[] { "staff_id", "year" },
                unique: true,
                filter: "[attendance_leave_type] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_artisan_applications_artisans_artisan_id",
                table: "artisan_applications",
                column: "artisan_id",
                principalTable: "artisans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_artisan_applications_product_categories_category_id",
                table: "artisan_applications",
                column: "category_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_artisans_product_categories_category_id",
                table: "artisans",
                column: "category_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_records_staff_staff_id",
                table: "attendance_records",
                column: "staff_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_leave_entitlements_staff_staff_id",
                table: "leave_entitlements",
                column: "staff_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_password_reset_tokens_logins_login_id",
                table: "password_reset_tokens",
                column: "login_id",
                principalTable: "logins",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_products_product_id",
                table: "sale_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_sales_entries_sales_entry_id",
                table: "sale_items",
                column: "sales_entry_id",
                principalTable: "sales_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_staff_leave_entries_staff_staff_id",
                table: "staff_leave_entries",
                column: "staff_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_artisans_current_artisan_id",
                table: "stall_reallocations",
                column: "current_artisan_id",
                principalTable: "artisans",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_artisans_new_artisan_id",
                table: "stall_reallocations",
                column: "new_artisan_id",
                principalTable: "artisans",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_stall_reallocations_stalls_stall_id",
                table: "stall_reallocations",
                column: "stall_id",
                principalTable: "stalls",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stalls_artisans_artisan_id",
                table: "stalls",
                column: "artisan_id",
                principalTable: "artisans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stalls_product_categories_category_id",
                table: "stalls",
                column: "category_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_products_product_id",
                table: "stock_movements",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_logins_login_id",
                table: "user_sessions",
                column: "login_id",
                principalTable: "logins",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
