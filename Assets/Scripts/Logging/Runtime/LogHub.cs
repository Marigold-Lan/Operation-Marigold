using System;
using System.Collections.Generic;
using OperationMarigold.Logging.Domain;

namespace OperationMarigold.Logging.Runtime
{
    /// <summary>
    /// 日志中枢：存储海量日志（环形缓冲）并推送新日志。
    /// 业务系统不应直接依赖此类；由适配层负责 Publish。
    /// </summary>
    public static class LogHub
    {
        public static event Action<LogEntry> OnPublished;

        private static readonly object _gate = new object();
        private static LogEntry[] _buffer = new LogEntry[DefaultCapacity];
        private static int _capacity = DefaultCapacity;
        private static int _count;
        private static int _head; // oldest index
        private static long _sequence;

        public const int DefaultCapacity = 50000;

        public static int Capacity
        {
            get
            {
                lock (_gate) return _capacity;
            }
        }

        public static int Count
        {
            get
            {
                lock (_gate) return _count;
            }
        }

        public static void SetCapacity(int capacity)
        {
            capacity = Math.Max(256, capacity);
            lock (_gate)
            {
                if (capacity == _capacity)
                    return;

                var snapshot = GetSnapshotNoLock();
                _buffer = new LogEntry[capacity];
                _capacity = capacity;
                _head = 0;
                _count = 0;
                for (var i = 0; i < snapshot.Count; i++)
                    AppendNoLock(snapshot[i]);
            }
        }

        public static void Publish(LogChannel channel, string message)
        {
            Publish(new LogEntry(0, DateTime.UtcNow, channel, message));
        }

        public static void Publish(in LogEntry entry)
        {
            LogEntry published;
            lock (_gate)
            {
                var seq = ++_sequence;
                published = new LogEntry(seq, entry.UtcTime == default ? DateTime.UtcNow : entry.UtcTime, entry.Channel, entry.Message);
                AppendNoLock(published);
            }

            OnPublished?.Invoke(published);
        }

        public static void Clear()
        {
            lock (_gate)
            {
                _buffer = new LogEntry[_capacity];
                _count = 0;
                _head = 0;
                _sequence = 0;
            }
        }

        public static List<LogEntry> GetSnapshot()
        {
            lock (_gate) return GetSnapshotNoLock();
        }

        private static void AppendNoLock(in LogEntry entry)
        {
            if (_count < _capacity)
            {
                var idx = (_head + _count) % _capacity;
                _buffer[idx] = entry;
                _count++;
                return;
            }

            // overwrite oldest
            _buffer[_head] = entry;
            _head = (_head + 1) % _capacity;
        }

        private static List<LogEntry> GetSnapshotNoLock()
        {
            var list = new List<LogEntry>(_count);
            for (var i = 0; i < _count; i++)
            {
                var idx = (_head + i) % _capacity;
                list.Add(_buffer[idx]);
            }
            return list;
        }
    }
}

