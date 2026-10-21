// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using System.Threading;
using ExtraPotions.Wildblood.Adopted.Util;
using Unity.Collections;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    internal static class FrameEncoder
    {
        private sealed class Job
        {
            internal NativeArray<byte> Pixels;
            internal int Width, Height;
            internal float Time, Keep;
            internal long Epoch;
        }
        private sealed class Worker
        {
            internal long Epoch;
            internal bool Busy;
            internal byte[] Picture = Array.Empty<byte>();
        }
        private static readonly object _lock = new object();
        private static readonly Queue<Job> _jobs = new Queue<Job>();
        private static readonly Stack<NativeArray<byte>> _pool = new Stack<NativeArray<byte>>();
        private static Worker? _worker;
        private static long _epoch;
        private static bool _accepting;
        public static volatile bool Failed;
        internal static long Epoch { get { lock (_lock) return _epoch; } }
        public static int Pending { get { lock (_lock) return _jobs.Count + (_worker?.Busy == true && _worker.Epoch == _epoch ? 1 : 0); } }

        internal static void Start()
        {
            lock (_lock) { if (_accepting) return; _accepting = true; ++_epoch; Failed = false; }
        }
        internal static void Stop()
        {
            lock (_lock)
            {
                if (!_accepting) return;
                _accepting = false; ++_epoch;
                while (_jobs.Count > 0) Dispose(_jobs.Dequeue().Pixels);
                while (_pool.Count > 0) Dispose(_pool.Pop());
                // The existing worker leaves at its next bounded boundary. Never start an overlapping encoder.
                Monitor.PulseAll(_lock);
            }
        }
        private static void Dispose(NativeArray<byte> buffer) { if (buffer.IsCreated) buffer.Dispose(); }
        public static NativeArray<byte> Rent(int size)
        {
            lock (_lock)
            {
                if (!_accepting) return default;
                while (_pool.Count > 0)
                {
                    NativeArray<byte> buffer = _pool.Pop();
                    if (buffer.Length == size) return buffer;
                    Dispose(buffer);
                }
                return new NativeArray<byte>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }
        public static void Return(NativeArray<byte> buffer, long epoch)
        {
            if (!buffer.IsCreated) return;
            lock (_lock)
            {
                if (_accepting && epoch == _epoch) _pool.Push(buffer);
                else Dispose(buffer);
            }
        }
        public static void Submit(NativeArray<byte> pixels, int width, int height, float time, float keep, long epoch)
        {
            lock (_lock)
            {
                if (!_accepting || epoch != _epoch) { Dispose(pixels); return; }
                _jobs.Enqueue(new Job { Pixels = pixels, Width = width, Height = height, Time = time, Keep = keep, Epoch = epoch });
                try { EnsureWorker(); }
                catch { DrainJobs(); throw; }
                Monitor.PulseAll(_lock);
            }
        }
        // Caller holds _lock. A retiring worker may still be in the native JPEG encoder, so new work waits boundedly.
        private static void EnsureWorker()
        {
            if (!_accepting || _worker != null || _jobs.Count == 0) return;
            var worker = _worker = new Worker { Epoch = _epoch };
            try { new Thread(() => Run(worker)) { IsBackground = true, Name = "Wildblood death recap encoder" }.Start(); }
            catch { _worker = null; throw; }
        }
        private static void DrainJobs() { while (_jobs.Count > 0) Dispose(_jobs.Dequeue().Pixels); }
        private static Job? Next(Worker worker)
        {
            lock (_lock)
            {
                while (_accepting && _epoch == worker.Epoch && _jobs.Count == 0) Monitor.Wait(_lock);
                if (!_accepting || _epoch != worker.Epoch) return null;
                worker.Busy = true;
                return _jobs.Dequeue();
            }
        }
        private static void Run(Worker worker)
        {
            try
            {
                Job? job;
                while ((job = Next(worker)) != null)
                {
                    try
                    {
                        byte[]? jpeg = JpegCodec.Encode(CopyOut(job, worker), job.Width, job.Height);
                        if (jpeg == null || jpeg.Length == 0) throw new InvalidOperationException("The encoder returned no picture.");
                        lock (_lock)
                            if (_accepting && _epoch == job.Epoch) FrameRing.Add(new Frame { Time = job.Time, Jpeg = jpeg }, job.Keep);
                    }
                    catch (Exception error)
                    {
                        lock (_lock)
                            if (_accepting && _epoch == job.Epoch && !Failed)
                            {
                                Failed = true;
                                Log.Warn("Death recap: video encoding stopped for this session (" + error.GetType().Name + "). Hits remain available.");
                            }
                    }
                    finally { lock (_lock) worker.Busy = false; }
                }
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_worker, worker)) _worker = null;
                    worker.Picture = Array.Empty<byte>();
                    try { EnsureWorker(); }
                    catch (Exception error) { Failed = true; DrainJobs(); Log.Warn("Death recap worker stopped: " + error.GetType().Name); }
                }
            }
        }
        private static byte[] CopyOut(Job job, Worker worker)
        {
            try
            {
                if (worker.Picture.Length != job.Pixels.Length) worker.Picture = new byte[job.Pixels.Length];
                job.Pixels.CopyTo(worker.Picture);
                return worker.Picture;
            }
            finally { Return(job.Pixels, job.Epoch); }
        }
    }
}
