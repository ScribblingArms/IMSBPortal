using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>Monthly pay bill: basic pay prorated by paid attendance days, plus allowances / overtime less deductions.</summary>
    public interface IPayrollInterface
    {
        /// <summary>The month's rows by department and name; query matches name or employee id.</summary>
        Task<List<PayBillRow>> RowsAsync(DateTime monthStart, string? query = null);

        Task<bool> IsGeneratedAsync(DateTime monthStart);

        /// <summary>Saves the month's pay bill with the given adjustments; false when a row names staff outside the month's pay bill.</summary>
        Task<bool> GenerateAsync(DateTime monthStart, IReadOnlyCollection<PayBillAdjustmentDto> rows);

        /// <summary>The employee's saved pay bills before the month, newest first.</summary>
        Task<List<PayBillHistoryRow>> HistoryAsync(Guid staffId, DateTime monthStart, int take);
    }
}
