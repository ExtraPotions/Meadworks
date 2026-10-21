// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder.Eating
{
    public enum FoodTopUpReason { Spawn, Retrieval, Automatic }

    public static class FoodTopUpRules
    {
        public static bool Allowed(FoodTopUpReason reason, bool onSpawnAndRetrieval, bool automatic)
        {
            switch (reason)
            {
                case FoodTopUpReason.Spawn:
                case FoodTopUpReason.Retrieval: return onSpawnAndRetrieval;
                case FoodTopUpReason.Automatic: return automatic;
                default: return false;
            }
        }
    }
}
