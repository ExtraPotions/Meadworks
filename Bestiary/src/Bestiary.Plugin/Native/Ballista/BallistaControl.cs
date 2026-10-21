// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Ballista/BallistaControl.cs).
// Changed by expDARE, 2026: Bestiary namespace; the game turret's own model and parts (BallistaParts); the holder stays
// where they took hold (no walking to the string), turned by the game to face where it points.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using ExtraPotions.Bestiary.Fixes;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using ExtraPotions.Bestiary.Runtime.Prefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Ballista
{
    /// <summary>
    /// The Bone Ballista's hold, on every peer (the dedicated server answers the requests). The use key asks the
    /// ballista's owner for it, as the game's ship helm does: the owner grants it when nobody valid holds it, records the
    /// holder in the ZDO and hands the ZDO over to them, so from then on the holder's own machine aims, reloads and
    /// shoots (<see cref="BallistaOperator"/>) and writes what everyone draws. The holder controls it through the game's
    /// doodad control (a ship's helm): movement keys do nothing, the mouse aims, attack shoots (<see cref="BallistaControls"/>),
    /// the use key, jump or dodge let go. Held, the player's hand items are put away and come back on letting go.
    /// The owner grants the request only when the sender is that player and they stand where it can be used.
    /// A grant is ignored on a client that did not ask.
    /// </summary>
    public sealed class BallistaControl : MonoBehaviour, Interactable, Hoverable, IDoodadController
    {
        private const string Request = "ecp_bal_request", Release = "ecp_bal_release", Answer = "ecp_bal_answer";
        private const float HolderRange = 6f;

        public ZNetView Net { get; private set; } = null!;
        public BallistaParts? Parts { get; private set; }

        /// <summary>The holder's own aim (degrees from where the ballista was placed), ahead of what the ZDO has sent.</summary>
        public float Yaw, Pitch;

        private bool holding;

        /// <summary>This client has asked for the ballista and is waiting for the owner's answer.</summary>
        private bool asked;

        private void Awake()
        {
            Net = GetComponent<ZNetView>();
            Parts = BallistaParts.Of(transform);
            if (Net == null || Net.GetZDO() == null)
            {
                return;   // a placement ghost
            }
            Net.Register<long>(Request, RPC_Request);
            Net.Register<long>(Release, RPC_Release);
            Net.Register<bool>(Answer, RPC_Answer);
        }

        private void FixedUpdate()
        {
            if (holding && Net.IsValid() && Player.m_localPlayer is Player player && player.GetDoodadController() == (IDoodadController)this)
            {
                SafeCall.Run("bone ballista hold", static (control, holder) => BallistaOperator.Tick(control, holder), this, player);
            }
        }

        private void OnDestroy()
        {
            if (holding && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.ShowHandItems();
            }
        }

        /// <summary>A fresh read of the ZDO; only on a valid object.</summary>
        public BallistaState State => BallistaState.Read(Net.GetZDO());

        /// <summary>Whether `player` holds this ballista on this machine.</summary>
        public bool HeldBy(Player player) => holding && player == Player.m_localPlayer && player.GetDoodadController() == (IDoodadController)this;

        public bool Interact(Humanoid character, bool hold, bool alt)
        {
            if (hold || Net == null || !Net.IsValid() || Parts == null || character is not Player player)
            {
                return false;
            }
            if (HeldBy(player))
            {
                player.StopDoodadControl();
                return true;
            }
            if (Vector3.Distance(player.transform.position, transform.position) > BallistaGrant.UseRange)
            {
                return false;
            }
            if (transform.InverseTransformPoint(player.transform.position).z > BallistaGrant.Behind)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_ecp_bal_behind");
                return false;
            }
            asked = true;
            Net.InvokeRPC(Request, player.GetPlayerID());
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>The owner: the ballista goes to the asker when that peer is the player, they are at it, and nobody valid holds it.</summary>
        private void RPC_Request(long sender, long playerID)
        {
            if (!Net.IsOwner())
            {
                return;
            }
            long senderPlayer = SenderPlayerId(sender);
            long user = Net.GetZDO().GetLong(BallistaKeys.User, 0L);
            bool blocked = user != playerID && ValidUser(user);
            if (!TryPosition(sender, senderPlayer, out Vector3 at)
                || !BallistaGrant.AcceptRequest(senderPlayer, playerID, Vector3.Distance(at, transform.position), transform.InverseTransformPoint(at).z, blocked))
            {
                if (senderPlayer != 0L && senderPlayer == playerID)
                {
                    Net.InvokeRPC(sender, Answer, false);
                }
                return;
            }
            Net.GetZDO().Set(BallistaKeys.User, senderPlayer);
            ZDOMan.instance.ForceSendZDO(sender, Net.GetZDO().m_uid);
            Net.GetZDO().SetOwner(sender);
            Net.InvokeRPC(sender, Answer, true);
        }

        private void RPC_Answer(long sender, bool granted)
        {
            if (!BallistaGrant.AcceptGrant(asked))
            {
                return;
            }
            asked = false;
            Player? player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            BallistaState state = State;
            (Yaw, Pitch, holding) = (state.Yaw, state.Pitch, true);
            player.StartDoodadControl(this);
            player.HideHandItems(false, false);
        }

        /// <summary>The owner (whoever has the ZDO now): the holder lets go. The sender must be that holder.</summary>
        private void RPC_Release(long sender, long playerID)
        {
            if (!Net.IsOwner())
            {
                return;
            }
            long holder = Net.GetZDO().GetLong(BallistaKeys.User, 0L);
            if (BallistaGrant.AcceptRelease(SenderPlayerId(sender), playerID, holder))
            {
                BallistaOperator.LetGo(this);
            }
        }

        /// <summary>The player id of the peer who sent the RPC. A call this machine routed to itself is the local player.</summary>
        private static long SenderPlayerId(long sender)
        {
            if (ZNet.instance != null)
            {
                ZNetPeer? peer = ZNet.instance.GetPeer(sender);
                if (peer != null)
                {
                    if (peer.m_playerID != 0L)
                    {
                        return peer.m_playerID;
                    }
                    if (ZDOMan.instance != null && !peer.m_characterID.IsNone())
                    {
                        ZDO? character = ZDOMan.instance.GetZDO(peer.m_characterID);
                        if (character != null)
                        {
                            long id = character.GetLong(ZDOVars.s_playerID, 0L);
                            if (id != 0L)
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            if (ZRoutedRpc.instance != null && sender == ZRoutedRpc.instance.m_id && Player.m_localPlayer != null)
            {
                return Player.m_localPlayer.GetPlayerID();
            }
            return 0L;
        }

        /// <summary>Where that player is standing. The loaded player wins; otherwise the peer's reference position.</summary>
        private static bool TryPosition(long sender, long playerId, out Vector3 position)
        {
            Player? player = Player.GetPlayer(playerId);
            if (player != null)
            {
                position = player.transform.position;
                return true;
            }
            if (ZNet.instance != null)
            {
                ZNetPeer? peer = ZNet.instance.GetPeer(sender);
                if (peer != null)
                {
                    position = peer.m_refPos;
                    return true;
                }
            }
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == playerId)
            {
                position = Player.m_localPlayer.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>A holder who is still in the world, alive and at the ballista.</summary>
        private bool ValidUser(long user)
        {
            Player? player = user != 0L ? Player.GetPlayer(user) : null;
            return player != null && !player.IsDead() && Vector3.Distance(player.transform.position, transform.position) < HolderRange;
        }

        public void OnUseStop(Player player)
        {
            holding = false;
            player.ShowHandItems();
            if (!Net.IsValid())
            {
                return;
            }
            if (Net.IsOwner())
            {
                BallistaOperator.LetGo(this);
            }
            else
            {
                Net.InvokeRPC(Release, player.GetPlayerID());
            }
        }

        public void ApplyControlls(Vector3 moveDir, Vector3 lookDir, bool run, bool autoRun, bool block)
        {
        }

        /// <summary>The turning neck: the game turns the holder to face where it points.</summary>
        public Component GetControlledComponent() => Parts != null ? Parts.Neck : transform;

        public Vector3 GetPosition() => transform.position;

        public bool IsValid() => this != null && Net != null && Net.IsValid();

        public string GetHoverName() => Localization.instance.Localize("$" + BallistaPiece.Word);

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            if (Net == null || !Net.IsValid())
            {
                return "";
            }
            BallistaState state = State;
            var text = new StringBuilder("$" + BallistaPiece.Word);
            text.Append(state.Settled == Spring.Loaded ? " ($hud_ecp_bal_loaded)" : " ($hud_ecp_bal_empty)");
            bool mine = Player.m_localPlayer != null && HeldBy(Player.m_localPlayer);
            if (mine || !ValidUser(state.User))
            {
                text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] ").Append(mine ? "$hud_ecp_bal_letgo" : "$hud_ecp_bal_hold");
            }
            return Localization.instance.Localize(text.ToString());
        }
    }
}
