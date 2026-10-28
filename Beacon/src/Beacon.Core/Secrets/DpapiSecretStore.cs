// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Windows Data Protection through the public crypt32 API (design 4.2), called directly so Beacon does not
// depend on Unity Mono's System.Security.Cryptography.ProtectedData.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ExtraPotions.Beacon.Secrets
{
    /// <summary>
    /// Secrets protected with <c>CryptProtectData</c> for the current Windows user, with a Beacon entropy value and the
    /// UI-forbidden flag. The blobs live base64 in a text file (<c>secrets.dat</c>), one <c>reference=blob</c> line each.
    /// A blob only opens for the same user on the same machine; elsewhere <see cref="TryRead"/> fails with
    /// <see cref="SecretFailure.StoredElsewhere"/> and the player is asked again.
    /// </summary>
    public sealed class DpapiSecretStore : ISecretStore
    {
        const int UiForbidden = 0x1;
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("com.meadworks.beacon/secrets/1");

        readonly Func<string?> readFile;
        readonly Action<string> writeFile;
        readonly SortedDictionary<string, string> blobs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        // Lines Load could not parse, kept byte for byte so the next save does not erase a blob that might be repaired.
        readonly List<string> unreadable = new List<string>();

        public DpapiSecretStore(Func<string?> readFile, Action<string> writeFile)
        {
            this.readFile = readFile ?? throw new ArgumentNullException(nameof(readFile));
            this.writeFile = writeFile ?? throw new ArgumentNullException(nameof(writeFile));
            Load();
        }

        public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public SecretBackend Backend => SecretBackend.WindowsDataProtection;
        public SecretAvailability Availability => IsSupported ? SecretAvailability.Available : SecretAvailability.Unavailable;
        public SecretFailure LastFailure { get; private set; }

        public bool Store(string reference, string secret)
        {
            SecretReference.Require(reference, secret);
            LastFailure = SecretFailure.None;
            if (!IsSupported) { LastFailure = SecretFailure.Unavailable; return false; }
            var plain = Encoding.UTF8.GetBytes(secret);
            try
            {
                var blob = Transform(plain, protect: true);
                if (blob == null) { LastFailure = SecretFailure.ProtectFailed; return false; }
                blobs[reference] = Convert.ToBase64String(blob);
                Save();
                return true;
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        public bool TryRead(string reference, out string secret)
        {
            SecretReference.Require(reference);
            secret = "";
            LastFailure = SecretFailure.None;
            if (!blobs.TryGetValue(reference, out var text)) return false;
            if (!IsSupported) { LastFailure = SecretFailure.Unavailable; return false; }
            var plain = Transform(Convert.FromBase64String(text), protect: false);
            if (plain == null) { LastFailure = SecretFailure.StoredElsewhere; return false; }
            try
            {
                secret = Encoding.UTF8.GetString(plain);
                return true;
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        public bool Contains(string reference) => SecretReference.IsValid(reference) && blobs.ContainsKey(reference);

        public bool Forget(string reference)
        {
            SecretReference.Require(reference);
            if (!blobs.Remove(reference)) return false;
            Save();
            return true;
        }

        public int ForgetAll()
        {
            var n = blobs.Count;
            blobs.Clear();
            unreadable.Clear();
            Save();
            return n;
        }

        void Load()
        {
            var text = readFile() ?? "";
            foreach (var raw in text.Split('\n'))
            {
                var preserved = raw.EndsWith("\r", StringComparison.Ordinal) ? raw.Substring(0, raw.Length - 1) : raw;
                var line = preserved.Trim();
                if (line.Length == 0) continue;
                var eq = line.IndexOf('=');
                var reference = eq > 0 ? line.Substring(0, eq) : "";
                var blob = eq > 0 ? line.Substring(eq + 1) : "";
                if (eq <= 0 || !SecretReference.IsValid(reference) || !IsBase64(blob)) { unreadable.Add(preserved); continue; }
                blobs[reference] = blob;
            }
        }

        void Save()
        {
            var b = new StringBuilder();
            foreach (var pair in blobs) b.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            foreach (var line in unreadable) b.Append(line).Append('\n');
            writeFile(b.ToString());
        }

        static bool IsBase64(string text)
        {
            if (text.Length == 0) return false;
            try { Convert.FromBase64String(text); return true; }
            catch (FormatException) { return false; }
        }

        static byte[]? Transform(byte[] input, bool protect)
        {
            var inBlob = new DataBlob();
            var entropy = new DataBlob();
            var outBlob = new DataBlob();
            try
            {
                inBlob = DataBlob.From(input);
                entropy = DataBlob.From(Entropy);
                var ok = protect
                    ? CryptProtectData(ref inBlob, null, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob);
                if (!ok || outBlob.Data == IntPtr.Zero) return null;
                var result = new byte[outBlob.Size];
                Marshal.Copy(outBlob.Data, result, 0, outBlob.Size);
                return result;
            }
            finally
            {
                inBlob.Free(clear: true);
                entropy.Free(clear: false);
                if (outBlob.Data != IntPtr.Zero)
                {
                    Zero(outBlob.Data, outBlob.Size);
                    LocalFree(outBlob.Data);
                }
            }
        }

        static void Zero(IntPtr data, int size)
        {
            for (var i = 0; i < size; i++) Marshal.WriteByte(data, i, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob
        {
            public int Size;
            public IntPtr Data;

            public static DataBlob From(byte[] bytes)
            {
                var blob = new DataBlob { Size = bytes.Length, Data = Marshal.AllocHGlobal(Math.Max(1, bytes.Length)) };
                Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
                return blob;
            }

            public void Free(bool clear)
            {
                if (Data == IntPtr.Zero) return;
                if (clear) Zero(Data, Size);
                Marshal.FreeHGlobal(Data);
                Data = IntPtr.Zero;
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);
    }
}
