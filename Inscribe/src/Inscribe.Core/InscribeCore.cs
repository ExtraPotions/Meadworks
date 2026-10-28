// Inscribe - ExtraPotions Meadworks gear inscriptions. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Inscribe
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class InscribeCore
    {
        public const string PluginGuid = "com.meadworks.inscribe";
        public const string ProductName = "Inscribe";
        public const string ProductVersion = "0.2.9";
        public const string Family = "Fang";

        /// <summary>Localization namespace (design section 13): tokens <c>$meadworks_inscribe_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "inscribe";
        public const string TokenPrefix = "meadworks_inscribe_";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }

    /// <summary>A source of uniform draws in [0, 1) so every roll in Inscribe.Core is testable with fixed values.</summary>
    public interface IDice
    {
        float Value();
    }

    /// <summary>Fixed draws for tests and replays; repeats the last value when the list runs out.</summary>
    public sealed class FixedDice : IDice
    {
        private readonly float[] values;
        private int next;

        public FixedDice(params float[] values)
        {
            this.values = values == null || values.Length == 0 ? new[] { 0f } : values;
        }

        public float Value()
        {
            var v = values[next < values.Length ? next : values.Length - 1];
            next++;
            return v;
        }
    }
}
