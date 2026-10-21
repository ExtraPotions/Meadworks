// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Tables.Window;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The Rune Table in the world (rune-table.md section 5): hover text, the use key, and the open handshake the game's
    /// chests use. A use asks the ZDO owner (<c>ECF_RT_Open</c>); unless someone has it open there, the owner hands its
    /// ownership to the asker and answers yes (<c>ECF_RT_Opened</c>), so the opener writes the table's store directly
    /// while the window is open and nobody else can open it meanwhile. Destroyed (the hammer, damage), its owner drops
    /// the runes it holds (<see cref="RuneStash"/>). On every peer, dedicated server included.
    /// </summary>
    internal sealed class RuneTable : MonoBehaviour, Hoverable, Interactable
    {
        private const string OpenRpc = "ECF_RT_Open";
        private const string OpenedRpc = "ECF_RT_Opened";
        public const float UseDistance = 3f;

        private ZNetView? _view;

        public TableStore Store { get; private set; } = null!;

        public bool IsValid => _view != null && _view.IsValid();

        private void Awake()
        {
            _view = GetComponent<ZNetView>();
            Store = new TableStore(_view);
            if (_view == null || _view.GetZDO() == null)
            {
                return;
            }
            if (_view.IsOwner())
            {
                Store.MigrateLegacy();
            }
            _view.Register<long>(OpenRpc, RPC_Open);
            _view.Register<bool>(OpenedRpc, RPC_Opened);
            WearNTear wear = GetComponent<WearNTear>();
            if (wear != null)
            {
                wear.m_onDestroyed += () => RuneStash.DropAll(Store, transform.position);
            }
        }

        public string GetHoverText()
        {
            string name = TableWords.Name;
            if (!ModSettings.RuneTable.Value)
            {
                return global::Localization.instance.Localize(name + "\n<color=#888888>$ecf_table_off</color>");
            }
            return global::Localization.instance.Localize(name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName() => TableWords.Name;

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer || !IsValid)
            {
                return false;
            }
            if (!ModSettings.RuneTable.Value)
            {
                user.Message(MessageHud.MessageType.Center, "$ecf_table_off");
                return false;
            }
            if (!PrivateArea.CheckAccess(transform.position))
            {
                return true;
            }
            _view!.InvokeRPC(OpenRpc, Game.instance.GetPlayerProfile().GetPlayerID());
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // On the owner: refuse while this machine has it open for someone else, else hand the table over.
        private void RPC_Open(long sender, long playerId)
        {
            if (!_view!.IsOwner())
            {
                return;
            }
            if (TableWindow.IsOpenAt(this) && sender != ZNet.GetUID())
            {
                _view.InvokeRPC(sender, OpenedRpc, false);
                return;
            }
            ZDOMan.instance.ForceSendZDO(sender, _view.GetZDO().m_uid);
            _view.GetZDO().SetOwner(sender);
            _view.InvokeRPC(sender, OpenedRpc, true);
        }

        private void RPC_Opened(long sender, bool granted)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            TableWindow.Open(this);
        }

        /// <summary>Whether the local player stands close enough to keep using it.</summary>
        public bool InReach(Player player) =>
            player != null && Vector3.Distance(player.transform.position, transform.position) <= UseDistance + 1f;
    }
}
