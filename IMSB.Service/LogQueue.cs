using System.Threading.Channels;

namespace IMSB.LoggerLibrary
{
    /// <summary>
    /// Hands entries from the loggers (any request thread) to <see cref="DatabaseLogWriter"/>. Bounded: when the
    /// database is slow or down the oldest entries are dropped, so logging never blocks or grows memory without limit.
    /// </summary>
    internal sealed class LogQueue
    {
        private readonly Channel<LogEntry> _channel;

        public LogQueue(DatabaseLoggerSettings settings)
        {
            _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(Math.Max(100, settings.QueueSize))
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Enqueue(LogEntry entry) => _channel.Writer.TryWrite(entry);

        public ChannelReader<LogEntry> Reader => _channel.Reader;

        /// <summary>No more entries (shutdown); the writer drains what is left.</summary>
        public void Complete() => _channel.Writer.TryComplete();
    }
}
