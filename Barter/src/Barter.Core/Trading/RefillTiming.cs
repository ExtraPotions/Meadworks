// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>Thread-local counters collected only inside an explicitly enabled morning refill.</summary>
    public static class RefillTiming
    {
        public enum Operation { AuthorityRead, AuthorityWrite, LocalRead, LocalWrite, CloudRead, CloudWrite, Promote, Flush, CloudFinish }
        public struct Cost { public long Ticks; public int Count; }
        public struct Result
        {
            public long TotalTicks;
            public int Day, Scanned, Traders, Refilled;
            public Cost AuthorityRead, AuthorityWrite, LocalRead, LocalWrite, CloudRead, CloudWrite, Promote, Flush, CloudFinish;
        }
        [ThreadStatic] private static bool active;
        [ThreadStatic] private static long started;
        [ThreadStatic] private static Result result;

        public static void Begin()
        {
            result = default;
            started = Stopwatch.GetTimestamp();
            active = true;
        }
        public static Result End()
        {
            result.TotalTicks = Stopwatch.GetTimestamp() - started;
            active = false;
            return result;
        }
        public static void SetDay(int day) { if (active) result.Day = day; }
        public static void Scanned() { if (active) result.Scanned++; }
        public static void Trader() { if (active) result.Traders++; }
        public static void Refilled() { if (active) result.Refilled++; }
        public static Span Measure(Operation operation) => new Span(operation, active);

        public readonly struct Span : IDisposable
        {
            private readonly Operation operation;
            private readonly long start;
            private readonly bool enabled;
            internal Span(Operation operation, bool enabled)
            {
                this.operation = operation;
                this.enabled = enabled;
                start = enabled ? Stopwatch.GetTimestamp() : 0;
            }
            public void Dispose()
            {
                if (!enabled) return;
                long ticks = Stopwatch.GetTimestamp() - start;
                switch (operation)
                {
                    case Operation.AuthorityRead: Add(ref result.AuthorityRead, ticks); break;
                    case Operation.AuthorityWrite: Add(ref result.AuthorityWrite, ticks); break;
                    case Operation.LocalRead: Add(ref result.LocalRead, ticks); break;
                    case Operation.LocalWrite: Add(ref result.LocalWrite, ticks); break;
                    case Operation.CloudRead: Add(ref result.CloudRead, ticks); break;
                    case Operation.CloudWrite: Add(ref result.CloudWrite, ticks); break;
                    case Operation.Promote: Add(ref result.Promote, ticks); break;
                    case Operation.Flush: Add(ref result.Flush, ticks); break;
                    case Operation.CloudFinish: Add(ref result.CloudFinish, ticks); break;
                }
            }
        }
        private static void Add(ref Cost cost, long ticks) { cost.Ticks += ticks; cost.Count++; }
    }
}
