// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Recap
{
    /// <summary>
    /// The game's own JPEG encoder and image decoder, <c>ImageConversion.EncodeArrayToJPG</c> (safe to call from any
    /// thread) and <c>ImageConversion.LoadImage</c> (main thread). Their module is built against netstandard 2.1, which a
    /// net48 project cannot reference, so both are found once at runtime and bound to delegates.
    /// </summary>
    internal static class JpegCodec
    {
        private const int Quality = 75;

        private delegate byte[] EncodeArray(Array array, GraphicsFormat format, uint width, uint height, uint rowBytes, int quality);
        private delegate bool LoadImage(Texture2D texture, byte[] data, bool markNonReadable);

        private static readonly Type? Conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        private static readonly EncodeArray? _encode = Bind<EncodeArray>("EncodeArrayToJPG",
            typeof(Array), typeof(GraphicsFormat), typeof(uint), typeof(uint), typeof(uint), typeof(int));
        private static readonly LoadImage? _load = Bind<LoadImage>("LoadImage", typeof(Texture2D), typeof(byte[]), typeof(bool));

        public static bool Available => _encode != null && _load != null;

        /// <summary>RGBA pixels, bottom row first as a texture holds them, to a JPEG. Any thread.</summary>
        public static byte[]? Encode(byte[] rgba, int width, int height) =>
            _encode?.Invoke(rgba, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0u, Quality);

        /// <summary>A JPEG into <paramref name="texture"/>, which takes its size. Main thread only.</summary>
        public static bool Decode(Texture2D texture, byte[] jpeg) => _load != null && _load(texture, jpeg, false);

        private static T? Bind<T>(string name, params Type[] parameters) where T : Delegate
        {
            MethodInfo? method = Conversion?.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            return method != null ? (T)Delegate.CreateDelegate(typeof(T), method) : null;
        }
    }
}
