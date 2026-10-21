// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// Records the screen while the local player is alive (and a moment after a death): at the end of a frame, at most
    /// the chosen frames per second, the finished screen is shrunk on the GPU (<see cref="ScreenGrab"/>) and read back
    /// without stalling (<c>AsyncGPUReadback</c>) straight into a reused native buffer, which the encoder's thread copies
    /// out and encodes - nothing of the picture is copied on the game's own thread. A frame is skipped rather than queued
    /// when the GPU or the encoder falls behind. Nothing records while the recap window is open (it would record itself),
    /// while the game is paused, on a machine without graphics or with recording turned off.
    /// </summary>
    internal sealed class FrameGrabber : MonoBehaviour
    {
        private const int MaxInFlight = 3;
        private const int MaxPending = 6;

        private readonly WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();
        private readonly ScreenGrab _grab = new ScreenGrab();
        private float _next;
        private int _inFlight;
        private bool _retired;
        private Coroutine? _loop;

        // A capture that failed once is reported once and not tried again this session.
        private static bool _broken;

        /// <summary>Pictures asked of the GPU and not yet read back; a recap waits for these too.</summary>
        public static int InFlight { get; private set; }

        private void OnEnable() { _retired = false; _broken = false; _loop = StartCoroutine(Loop()); }

        private void OnDisable()
        {
            _retired = true;
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            if (_inFlight == 0) _grab.Release();
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return _endOfFrame;
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Guard.Report(e, "death recap recording (stopped for this session; deaths are kept without video)");
                }
            }
        }

        private void Tick()
        {
            if (!ShouldRecord() || Time.unscaledTime < _next || InFlight >= MaxInFlight || FrameEncoder.Pending >= MaxPending)
            {
                return;
            }
            int height = Mathf.Clamp(RecapSettings.Height.Value, 180, 720);
            int width = Mathf.Min(Screen.width, Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height))) & ~1;
            // A pending read owns its exact video surface until its callback, including during resolution/config changes.
            if (_inFlight != 0 && (_grab.Height != height || _grab.Width != width)) return;
            _next = Time.unscaledTime + 1f / Mathf.Clamp(RecapSettings.FramesPerSecond.Value, 5, 30);
            Request(_grab.Capture(height), Time.time);
        }

        // The picture is read straight into a buffer the encoder owns from then on; one the GPU never got is given back.
        private void Request(RenderTexture video, float time)
        {
            int width = video.width;
            int height = video.height;
            NativeArray<byte> pixels = FrameEncoder.Rent(width * height * 4);
            if (!pixels.IsCreated) return;
            long epoch = FrameEncoder.Epoch;
            InFlight++; _inFlight++;
            try
            {
                AsyncGPUReadback.RequestIntoNativeArray(ref pixels, video, 0, TextureFormat.RGBA32,
                    request => Arrived(request, pixels, width, height, time, epoch));
            }
            catch
            {
                InFlight = Mathf.Max(0, InFlight - 1);
                _inFlight = Mathf.Max(0, _inFlight - 1);
                FrameEncoder.Return(pixels, epoch);
                if (_retired && _inFlight == 0) _grab.Release();
                throw;
            }
        }

        private void Arrived(AsyncGPUReadbackRequest request, NativeArray<byte> pixels, int width, int height, float time, long epoch)
        {
            InFlight = Mathf.Max(0, InFlight - 1);
            _inFlight = Mathf.Max(0, _inFlight - 1);
            bool handedOff = false;
            try
            {
                if (request.hasError || _retired || !ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording || epoch != FrameEncoder.Epoch)
                { handedOff = true; FrameEncoder.Return(pixels, epoch); return; }
                float keep = RecapSettings.Seconds.Value + RecapStore.Tail + 1f;
                handedOff = true; // Submit owns the native buffer even when starting its worker fails.
                FrameEncoder.Submit(pixels, width, height, time, keep, epoch);
            }
            catch (Exception error)
            {
                if (!handedOff) FrameEncoder.Return(pixels, epoch);
                Guard.Report(error, "death recap late frame");
            }
            finally { if (_retired && _inFlight == 0) _grab.Release(); }
        }

        private static bool ShouldRecord() =>
            ExtraPotions.Wildblood.Runtime.WildbloodRecap.Recording
            && RecapStore.Recording
            && !ExtraPotions.Wildblood.Runtime.WildbloodRecap.IsOpen
            && !Game.IsPaused()
            && Supported;

        private static bool Supported =>
            !_broken
            && !FrameEncoder.Failed
            && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
            && SystemInfo.supportsAsyncGPUReadback
            && JpegCodec.Available;
    }
}
