// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What a player sees of Fixated, on every client and never on a dedicated server: a red eye over the marked player's
    /// head that follows them, and one chat line each time the mark lands or moves ("Bonemass fixes on Gerald!", or "on
    /// you!" for the player it lands on). The eye is the game's own - the one it shows over a creature that has noticed
    /// you - copied onto the game's nameplate layer, so it hides with the HUD, and turned red. Both are read from the mark
    /// in the boss's ZDO each frame, so nothing is sent: every client holding the boss sees the change as the ZDO
    /// arrives. The line is said only for a mark that landed moments ago, so walking up to a fight already under way, or
    /// the boss changing hands, says nothing; the eye shows who is marked all the same. Both only near the boss.
    /// </summary>
    internal sealed class FixatedView
    {
        /// <summary>How near the boss this player must be to see the eye and the line, in metres (the boss bar's own reach).</summary>
        private const float ViewRange = 100f;

        /// <summary>How long a new mark is still news, in seconds.</summary>
        private const float Fresh = 10f;

        /// <summary>From the head point to the eye, in the HUD's units: just above another player's name tag.</summary>
        private const float Lift = 76f;

        private static readonly Color Red = new Color(1f, 0.25f, 0.2f, 1f);

        private readonly Character _boss;
        private readonly ZNetView _nview;
        private ZDOID _shown = ZDOID.None;
        private Player? _marked;
        private RectTransform? _eye;

        public FixatedView(Character boss, ZNetView nview)
        {
            _boss = boss;
            _nview = nview;
        }

        /// <summary>Every frame, after the camera has moved.</summary>
        public void Draw()
        {
            ZDO? zdo = _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
            ZDOID mark = zdo != null && !_boss.IsDead() ? FixatedMark.Get(zdo) : ZDOID.None;
            if (mark != _shown)
            {
                _shown = mark;
                _marked = null;
                if (mark != ZDOID.None && NearBoss() && FixatedMark.SecondsSinceLanded(zdo!) < Fresh)
                {
                    Announce(mark);
                }
            }
            if (_marked == null && _shown != ZDOID.None)
            {
                _marked = FixatedMark.Resolve(_shown); // again each frame until this client has them loaded
            }
            Place(_marked);
        }

        public void Dispose()
        {
            if (_eye != null)
            {
                Object.Destroy(_eye.gameObject);
            }
        }

        private bool NearBoss()
        {
            Player local = Player.m_localPlayer;
            return local != null && Vector3.Distance(local.transform.position, _boss.transform.position) <= ViewRange;
        }

        private void Announce(ZDOID mark)
        {
            Chat chat = Chat.instance;
            if (chat == null)
            {
                return;
            }
            Player local = Player.m_localPlayer;
            string who = local != null && local.GetZDOID() == mark ? "you" : FixatedMark.NameOf(mark);
            string boss = global::Localization.instance != null ? global::Localization.instance.Localize(_boss.m_name) : _boss.m_name;
            chat.AddString($"<color=#FF5A45>{boss} fixes on <noparse>{who}</noparse>!</color>");
            chat.m_hideTimer = 0f; // open the chat window the way a spoken line does
        }

        // Screen-space like the game's own nameplates: moved to the head's screen point each frame, hidden behind the camera.
        private void Place(Player? marked)
        {
            Camera camera = Utils.GetMainCamera();
            bool show = marked != null && camera != null && NearBoss();
            Vector3 point = show ? camera!.WorldToScreenPointScaled(marked!.GetHeadPoint() + Vector3.up * 0.3f) : Vector3.zero;
            show &= point.z > 0f;
            RectTransform? eye = show ? Eye() : _eye;
            if (eye == null)
            {
                return;
            }
            eye.gameObject.SetActive(show);
            if (show)
            {
                eye.position = point;
                eye.localPosition += Vector3.up * Lift;
            }
        }

        private RectTransform? Eye()
        {
            if (_eye != null)
            {
                return _eye;
            }
            EnemyHud hud = EnemyHud.instance;
            Transform? aware = hud != null && hud.m_baseHud != null ? hud.m_baseHud.transform.Find("Aware") : null;
            if (aware == null || hud!.m_hudRoot == null)
            {
                return null;
            }
            GameObject copy = Object.Instantiate(aware.gameObject, hud.m_hudRoot.transform);
            copy.name = "ecr_fixated_eye";
            Image image = copy.GetComponent<Image>();
            if (image != null)
            {
                image.color = Red;
            }
            _eye = (RectTransform)copy.transform;
            return _eye;
        }
    }
}
