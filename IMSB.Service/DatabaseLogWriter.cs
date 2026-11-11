using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace IMSB.LoggerLibrary
{
    /// <summary>
    /// Background service that empties the <see cref="LogQueue"/> into FW.TblLog with SqlBulkCopy, one batch per
    /// flush interval. A failed write drops that batch and is reported on the console (at most every five minutes):
    /// logging must never take the application down or log about itself in a loop.
    /// </summary>
    internal sealed class DatabaseLogWriter : BackgroundService
    {
        private static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ShutdownFlush = TimeSpan.FromSeconds(5);

        private readonly LogQueue _queue;
        private readonly DatabaseLoggerSettings _settings;
        private DateTime _lastReported = DateTime.MinValue;
        private int _droppedSinceReport;

        public DatabaseLogWriter(LogQueue queue, DatabaseLoggerSettings settings)
        {
            _queue = queue;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<LogEntry>(_settings.BatchSize);
            try
            {
                while (await _queue.Reader.WaitToReadAsync(stoppingToken))
                {
                    // let a burst of entries collect, so it goes in one insert
                    await Task.Delay(_settings.FlushInterval, stoppingToken);
                    await DrainAsync(batch, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down: StopAsync writes what is left
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Complete();
            await base.StopAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ShutdownFlush);
            try { await DrainAsync(new List<LogEntry>(_settings.BatchSize), timeout.Token); }
            catch (OperationCanceledException) { }
        }

        private async Task DrainAsync(List<LogEntry> batch, CancellationToken token)
        {
            while (true)
            {
                batch.Clear();
                while (batch.Count < _settings.BatchSize && _queue.Reader.TryRead(out var entry)) batch.Add(entry);
                if (batch.Count == 0) return;
                await WriteAsync(batch, token);
            }
        }

        private async Task WriteAsync(List<LogEntry> batch, CancellationToken token)
        {
            try
            {
                using var table = new DataTable();
                table.Columns.Add("AppType", typeof(string));
                table.Columns.Add("Date", typeof(DateTime));
                table.Columns.Add("Level", typeof(string));
                table.Columns.Add("Message", typeof(string));
                table.Columns.Add("LogIP", typeof(string));
                table.Columns.Add("Context", typeof(string));
                foreach (var e in batch)
                    table.Rows.Add((object?)e.AppType ?? DBNull.Value, e.Date, e.Level, e.Message, (object?)e.LogIP ?? DBNull.Value, e.Context);

                await using var connection = new SqlConnection(_settings.ConnectionString);
                await connection.OpenAsync(token);
                using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "[FW].[TblLog]", BatchSize = batch.Count, BulkCopyTimeout = 30 };
                foreach (DataColumn column in table.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                await bulk.WriteToServerAsync(table, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // e.g. the database is down or FW.TblLog does not exist yet (migration AddGmcDbObjects not applied)
                _droppedSinceReport += batch.Count;
                if (DateTime.UtcNow - _lastReported >= ReportEvery)
                {
                    Console.Error.WriteLine($"IMSB.LoggerLibrary: could not write {_droppedSinceReport} log entries to FW.TblLog: {ex.Message}");
                    _lastReported = DateTime.UtcNow;
                    _droppedSinceReport = 0;
                }
            }
        }
    }
}
