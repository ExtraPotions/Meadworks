// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.RimeGiant
{
    /// <summary>
    /// Draws the rime armour on every machine from the plate count in the ZDO (<see cref="RimeArmour.Plates"/>): the
    /// first that many plates are on, the rest gone. A plate lost while this machine watches breaks off and tumbles
    /// away (<see cref="RimeShards"/>); one regained glitters back. What was already so when the giant loaded is shown
    /// as it is, without either.
    /// </summary>
    public class RimeArmourLook : MonoBehaviour
    {
        private const int KitPlates = 8;

        private RimeArmour _armour = null!;
        private GameObject?[] _plates = new GameObject?[0];
        private int _shown = -1;

        private void Awake()
        {
            _armour = GetComponent<RimeArmour>();
            _plates = RimeKit.Plates(transform, KitPlates);
        }

        private void Update()
        {
            int plates = _armour.Plates;
            if (plates == _shown)
            {
                return;
            }
            bool watched = _shown >= 0;
            _shown = plates;
            for (int i = 0; i < _plates.Length; i++)
            {
                Show(_plates[i], i < plates, watched);
            }
        }

        private void Show(GameObject? plate, bool on, bool watched)
        {
            if (plate == null || plate.activeSelf == on)
            {
                return;
            }
            if (watched && !on)
            {
                RimeShards.BreakOff(plate, transform);
            }
            plate.SetActive(on);
            if (watched && on)
            {
                RimeEffects.Grow(plate.transform.position);
            }
        }
    }
}
