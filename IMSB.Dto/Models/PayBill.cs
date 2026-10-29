using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

[Table("pay_bill_entries")]
public class PayBillEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("staff_id")]
    public Guid StaffId { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("month")]
    public int Month { get; set; }

    [Required, MaxLength(20)]
    [Column("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    [Column("employee_name")]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("department")]
    public string Department { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("designation")]
    public string Designation { get; set; } = string.Empty;

    [Column("month_days")]
    public int MonthDays { get; set; }

    [Column("paid_days", TypeName = "decimal(6,2)")]
    public decimal PaidDays { get; set; }

    [Column("monthly_salary", TypeName = "decimal(18,2)")]
    public decimal MonthlySalary { get; set; }

    [Column("basic_pay", TypeName = "decimal(18,2)")]
    public decimal BasicPay { get; set; }

    [Column("allowances", TypeName = "decimal(18,2)")]
    public decimal Allowances { get; set; }

    [Column("overtime_hours", TypeName = "decimal(8,2)")]
    public decimal OvertimeHours { get; set; }

    [Column("overtime_amount", TypeName = "decimal(18,2)")]
    public decimal OvertimeAmount { get; set; }

    [Column("deductions", TypeName = "decimal(18,2)")]
    public decimal Deductions { get; set; }

    [Column("net_pay", TypeName = "decimal(18,2)")]
    public decimal NetPay { get; set; }

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class GeneratePayBillDto
{
    [Range(2000, 9999)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Required, MinLength(1), MaxLength(2000)]
    public List<PayBillAdjustmentDto> Rows { get; set; } = new();
}

public class PayBillAdjustmentDto
{
    public Guid StaffId { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal Allowances { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal OvertimeAmount { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal Deductions { get; set; }
}
