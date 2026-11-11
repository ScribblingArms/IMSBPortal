using IMSB.Interfaces;

namespace IMSB.Models
{
    /// <summary>The pay bill printout and PDF (Payroll/Print, Payroll/_PayBillDocument).</summary>
    public sealed record PayBillPrintModel(string Title, string Period, List<PayBillRow> Rows, decimal TotalNetPay);
}
