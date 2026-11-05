using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models;

[Table("pay_bill_entries")]
public class PayBillEntry
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("StaffId")]
    public Guid StaffId { get; set; }

    [Column("Year")]
    public int Year { get; set; }

    [Column("Month")]
    public int Month { get; set; }

    [Required, MaxLength(20)]
    [Column("EmployeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    [Column("EmployeeName")]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("Department")]
    public string Department { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("Designation")]
    public string Designation { get; set; } = string.Empty;

    [Column("MonthDays")]
    public int MonthDays { get; set; }

    [Column("PaidDays", TypeName = "decimal(6,2)")]
    public decimal PaidDays { get; set; }

    [Column("MonthlySalary", TypeName = "decimal(18,2)")]
    public decimal MonthlySalary { get; set; }

    [Column("BasicPay", TypeName = "decimal(18,2)")]
    public decimal BasicPay { get; set; }

    [Column("Allowances", TypeName = "decimal(18,2)")]
    public decimal Allowances { get; set; }

    [Column("OvertimeHours", TypeName = "decimal(8,2)")]
    public decimal OvertimeHours { get; set; }

    [Column("OvertimeAmount", TypeName = "decimal(18,2)")]
    public decimal OvertimeAmount { get; set; }

    [Column("Deductions", TypeName = "decimal(18,2)")]
    public decimal Deductions { get; set; }

    [Column("NetPay", TypeName = "decimal(18,2)")]
    public decimal NetPay { get; set; }

    [Column("GeneratedAt")]
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
