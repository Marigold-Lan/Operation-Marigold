using System;

namespace OperationMarigold.Logging.Domain
{
    public readonly struct LogEntry
    {
        public readonly long Sequence;
        public readonly DateTime UtcTime;
        public readonly LogChannel Channel;
        public readonly string Message;

        public LogEntry(long sequence, DateTime utcTime, LogChannel channel, string message)
        {
            Sequence = sequence;
            UtcTime = utcTime;
            Channel = channel;
            Message = message ?? string.Empty;
        }
    }
}

