// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Watches the rule file the way ConfigReload watches the .cfg: a slow poll of the file's write time and size on
    /// the main thread, no file watcher. When the file changes it re-reads it and lets the server lock re-mirror and
    /// re-push. A reload never takes the game down - a bad file is reported and the last good rules stay in force.
    /// </summary>
    public sealed class RuleReload : MonoBehaviour
    {
        private const float IntervalSeconds = 5f;
        private float _timer;

        public static void Attach()
        {
            GameObject holder = new GameObject("ecr_rule_reload");
            DontDestroyOnLoad(holder);
            holder.AddComponent<RuleReload>();
        }

        private void Update() => Guard.Run("RuleReload.Update", static self => self.Poll(), this);

        private void Poll()
        {
            _timer += Time.deltaTime;
            if (_timer < IntervalSeconds)
            {
                return;
            }
            _timer = 0f;
            if (RuleFile.ChangedOnDisk() && RuleFile.Load())
            {
                ServerLock.OnLocalReloaded();
            }
        }
    }
}
