// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: secret store selection per platform (design section 4, Q1 (a)).
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BepInEx;
using ExtraPotions.Beacon.Config;
using ExtraPotions.Beacon.Secrets;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// Picks Beacon's secret store once: Windows Data Protection on Windows (<c>secrets.dat</c> in Beacon's data folder); on
    /// Linux the Secret Service keyring with the selected Linux fallback (Q1 (a): session only). A
    /// failure never falls back to a plain file. All Beacon log text passes through <see cref="Redactor"/>.
    /// </summary>
    public static class BeaconSecrets
    {
        public static ISecretStore Store { get; private set; } = new DisabledSecretStore();

        public static SecretRedactor Redactor { get; } = new SecretRedactor();
        static Task<bool>? refresh;
        static bool reportedFallback;
        static BeaconConfig? linuxSettings;

        public static void Initialize(BeaconConfig settings, MeadworksLog log)
        {
            var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SecretPlatform.Windows
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SecretPlatform.Linux : SecretPlatform.Other;
            if (platform == SecretPlatform.Linux)
            {
                linuxSettings = settings;
                var linux = new LinuxKeyringStore(settings.Get(BeaconSettings.Passwords.LinuxFallback));
                Store = linux;
                refresh = linux.RefreshAsync();
                reportedFallback = false;
                return;
            }
            var backend = SecretStores.Choose(platform, keyringUsable: false, settings.Get(BeaconSettings.Passwords.LinuxFallback));
            Store = backend switch
            {
                SecretBackend.WindowsDataProtection => new DpapiSecretStore(ReadSecretFile, WriteSecretFile),
                SecretBackend.Disabled => new DisabledSecretStore(),
                _ => MemorySecretStore.SessionOnly(),
            };
            log.Info("Beacon password storage: " + Store.Backend + ".");
        }

        public static string SecretFilePath => Path.Combine(BeaconServers.DataDirectory, BeaconCore.WindowsSecretFile);

        public static void Shutdown()
        {
            if (Store is IDisposable owned) owned.Dispose();
            Store = new DisabledSecretStore();
            refresh = null;
            linuxSettings = null;
            Redactor.Clear();
        }

        /// <summary>Main-thread result observation only; no keyring native call or task wait.</summary>
        public static void Tick(MeadworksLog log)
        {
            if (Store is LinuxKeyringStore linux && linuxSettings != null)
                linux.SetFallback(linuxSettings.Get(BeaconSettings.Passwords.LinuxFallback));
            if (refresh != null && refresh.IsCompleted)
            {
                if (refresh.IsFaulted) log.Warning("Beacon keyring initialization failed; no password was saved.");
                refresh = null;
            }
            if (Store is LinuxKeyringStore && Store.Backend != SecretBackend.LinuxKeyring && !reportedFallback)
            {
                reportedFallback = true;
                log.Warning(BeaconText.Text("secret_store_failed") + " " + BeaconText.Text(SecretStores.StorageLineKey(Store.Backend)));
            }
        }

        static string? ReadSecretFile() => File.Exists(SecretFilePath) ? File.ReadAllText(SecretFilePath) : null;

        static void WriteSecretFile(string text) => BeaconServers.WriteAtomically(SecretFilePath, text);
    }
}
