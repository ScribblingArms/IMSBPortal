namespace IMSB.LoggerLibrary
{
    /// <summary>One FW.TblLog row, already cut to the column sizes.</summary>
    internal sealed record LogEntry(
        string? AppType,   // varchar(50)
        DateTime Date,     // datetime, server local time like the other gmc_db tables
        string Level,      // varchar(10)
        string Message,    // varchar(1000)
        string? LogIP,     // varchar(20)
        string Context);   // varchar(max): JSON with category, exception, request and user
}
