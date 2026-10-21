// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Runtime
{
    /// <summary>
    /// The per-creature calls routed through a creature's own ZNetView, because a call addressed to the creature is
    /// handled only where the creature is loaded. One is an owner-directed command - "you devoured this, take it" (to the
    /// devourer's owner, where its ZDO and health live, and where the meal joins its meal list). The other is an effect
    /// tell - the Warding reflect sound, the devour tell and the other one-shot tells - sent to each player near the
    /// creature, so only the clients that could see it get it (unlike the world-wide bus in <see cref="EliteRpc"/>,
    /// whose calls every machine handles). Two more owner-directed commands serve the boss aspects: a Twin's share of its
    /// partner's lost health, and a Phantom copy's dismissal when its boss dies. (The prey pin is not here: a bite pins
    /// the prey directly on the prey's own owner, which is the same machine the hit resolves on, so it needs no
    /// routing.) Registered once per creature, each command only where it can be sent; sending to an object you already
    /// own is delivered locally, so a single-machine session uses one path.
    /// </summary>
    public static class CreatureRpc
    {
        public const string Devour = "ecr_devour";
        public const string Flash = "ecr_flash";
        public const string TwinShare = "ecr_twin_share";
        public const string VanishCall = "ecr_vanish";

        /// <summary>The flash role of the devour tell, as it travels over the effect bus.</summary>
        public const string DevourRole = "devour";

        /// <summary>The flash role of the reflect tell (Warding, and the Reflective boss aspect).</summary>
        public const string ReflectRole = "reflect";

        /// <summary>
        /// The reflect tell is only heard, at this share of its effect's own loudness: it plays on every hit a Warding
        /// creature or a Reflective boss takes, and drawn it cost frames in a long fight while its sound drowned the
        /// fight's own (the user asked for no particles and a quieter sound).
        /// </summary>
        private const float ReflectVolume = 0.3f;

        /// <summary>How near a player must be to be sent a tell: further than a flash or its sound carries in a fight.</summary>
        private const float TellRange = 100f;

        /// <summary>Registers the effect handler on a creature, on every machine, before it has resolved: a tell can
        /// reach a machine still waiting for the owner's roll.</summary>
        public static bool Register(ZNetView nview) =>
            // The flash handler runs on EVERY client holding the creature, drawing a pure cosmetic; nobody gates it.
            NativeRpcOwnership.View(nview, NativeRpcOwner.CreatureFlash, () =>
                nview.Register<Vector3, float, string>(Flash, (sender, pos, radius, role) => DrawFlash(pos, radius, role)), Flash);

        /// <summary>
        /// Registers the owner-directed commands a creature can receive, once its traits have resolved, and only those
        /// its traits can be sent: a devour for a devourer, a twin's share and a phantom's dismissal for a boss (twins and
        /// copies are bosses too), a steal for a thief. An ordinary creature registers none, so an area full of them
        /// loading at once builds no handlers it will never use. A command sent to a machine still waiting for the roll is
        /// dropped there; the owner rolled at once and has its handlers.
        /// </summary>
        public static void RegisterCommands(EliteController controller, bool boss)
        {
            ZNetView nview = controller.View;
            Character character = controller.Creature;
            if (controller.Traits.Has(Mutation.Devouring))
            {
                // The command handler runs on THIS creature's owner - that is where a no-target routed call is delivered.
                NativeRpcOwnership.View(nview, NativeRpcOwner.CreatureDevour, () => nview.Register<float, float, int>(Devour,
                    (sender, health, damage, meal) => ApplyAbsorb(character, controller, health, damage, meal)), Devour);
            }
            if (boss)
            {
                NativeRpcOwnership.View(nview, NativeRpcOwner.CreatureBoss, () => {
                    nview.Register<float, bool>(TwinShare, (sender, loss, fatal) => ReceiveShare(character, loss, fatal));
                    nview.Register(VanishCall, sender => Guard.Run("CreatureRpc.Vanish", () => Fall(character)));
                }, TwinShare, VanishCall);
            }
            if (controller.Traits.Has(Mutation.Thieving))
            {
                ThievingRpc.Register(nview, controller); // the robbed player's client routes a steal to the owner
            }
        }

        private static void ReceiveShare(Character character, float loss, bool fatal) =>
            Guard.Run("CreatureRpc.TwinShare", () => ShareInto(character, loss, fatal));

        private static void ShareInto(Character character, float loss, bool fatal)
        {
            if (!NativeRpcOwnership.OwnsView(character.m_nview, NativeRpcOwner.CreatureBoss)) return;
            TwinLink link = character.GetComponent<TwinLink>();
            if (link != null)
            {
                link.Receive(loss, fatal);
            }
        }

        /// <summary>Boss-owner side, when a Phantom boss dies: routes the copy's dismissal to whichever machine owns it.</summary>
        public static void Vanish(Character copy)
        {
            ZNetView nview = copy.GetComponent<ZNetView>();
            if (nview != null && NativeRpcOwnership.OwnsView(nview, NativeRpcOwner.CreatureBoss))
            {
                nview.InvokeRPC(VanishCall); // targets the copy's owner; delivered locally when that is this machine
            }
        }

        // On the copy's owner: health to zero, so the game's own death check lets it fall - hollow, so no drops, no body.
        private static void Fall(Character copy)
        {
            ZNetView nview = copy.GetComponent<ZNetView>();
            if (nview != null && NativeRpcOwnership.OwnsView(nview, NativeRpcOwner.CreatureBoss) && nview.IsOwner() && !copy.IsDead())
            {
                copy.SetHealth(0f);
            }
        }

        /// <summary>
        /// Effect tell through the creature's own ZNetView, sent only to the players within <see cref="TellRange"/> of
        /// it - each one's own machine, found from the players loaded here (a player's ZDO is owned by that player's
        /// machine) - rather than to everybody: it goes out on every hit a Warding creature takes, and a player three
        /// biomes away would only drop it unread. The local player's copy is delivered locally; a dedicated server has
        /// no player of its own and sends only to the players near the creature.
        /// </summary>
        public static void FireFlash(ZNetView creatureView, Vector3 pos, float radius, string role)
        {
            if (!NativeRpcOwnership.OwnsView(creatureView, NativeRpcOwner.CreatureFlash))
            {
                return;
            }
            foreach (Player player in Player.GetAllPlayers())
            {
                ZDO? zdo = player.m_nview != null && player.m_nview.IsValid() ? player.m_nview.GetZDO() : null;
                if (zdo != null && zdo.GetOwner() != 0L && (player.transform.position - pos).sqrMagnitude <= TellRange * TellRange)
                {
                    creatureView.InvokeRPC(zdo.GetOwner(), Flash, pos, radius, role ?? "");
                }
            }
        }

        /// <summary>The reflect tell at the attacker, the same way; it carries no size, since nothing is drawn.</summary>
        public static void FireReflect(ZNetView creatureView, Vector3 pos) => FireFlash(creatureView, pos, 0f, ReflectRole);

        /// <summary>
        /// The devour tell at the prey, through the DEVOURER's view, so every client that can see the fight hears the
        /// creature eaten. Only heard (the user asked for no particles), so it carries no size either.
        /// </summary>
        public static void FireDevour(ZNetView devourerView, Vector3 pos) => FireFlash(devourerView, pos, 0f, DevourRole);

        // A dedicated server holds every creature near a player, so it gets every tell; it has no one to show it to.
        private static void DrawFlash(Vector3 pos, float radius, string role)
        {
            if (!Machine.Headless)
            {
                Guard.Run("CreatureRpc.Flash", static tell => DrawRole(tell.pos, tell.radius, tell.role), (pos, radius, role));
            }
        }

        private static void DrawRole(Vector3 pos, float radius, string role)
        {
            GameObject? prefab = EffectResolver.ForRole(role);
            if (role == ReflectRole || role == DevourRole)
            {
                CosmeticClone.SoundOnly(prefab, pos, role == ReflectRole ? ReflectVolume : 1f);
                return;
            }
            CosmeticClone.Flash(prefab, pos, radius);
        }

        /// <summary>
        /// Devourer-owner-side: bank a kill's health and damage, and the eaten creature's prefab (<paramref name="meal"/>)
        /// in its meal list. Routed to the owner from wherever the kill resolved.
        /// </summary>
        public static void Absorb(Character killer, float health, float damage, int meal)
        {
            ZNetView nview = killer.GetComponent<ZNetView>();
            if (!NativeRpcOwnership.OwnsView(nview, NativeRpcOwner.CreatureDevour))
            {
                return;
            }
            if (nview.IsOwner())
            {
                ApplyAbsorb(killer, killer.GetComponent<EliteController>(), health, damage, meal);
                return;
            }
            nview.InvokeRPC(Devour, health, damage, meal); // targets the ZDO owner
        }

        private static void ApplyAbsorb(Character killer, EliteController controller, float health, float damage, int meal) =>
            Guard.Run("CreatureRpc.Absorb", () => Bank(killer, controller, health, damage, meal));

        // The owner re-checks the allowance before banking: one swing can bite two creatures before the first meal is
        // banked, and a meal past the allowance is not kept - the prey is gone, but the devourer gains nothing from it.
        private static void Bank(Character killer, EliteController controller, float health, float damage, int meal)
        {
            if (killer == null || controller == null || !controller.IsOwner())
            {
                return;
            }
            ZDO zdo = controller.View.GetZDO();
            if (DevourLimits.Sated(controller))
            {
                if (Log.Diagnostics)
                {
                    Log.Diag($"{killer.name} has eaten its {MealStore.Count(zdo)}; this meal is not banked");
                }
                return;
            }
            MealStore.Add(zdo, meal);
            TraitStore.AddDevoured(zdo, health, damage);
            StartCooldown(controller, zdo);
            controller.RefreshHealth();
            killer.Heal(health, showText: false);
        }

        // The meal is banked, so the cooldown starts here on the devourer's owner: it cannot eat again until this instant.
        // Stored as whole milliseconds off the shared clock, so every machine reads the same deadline for the next bite.
        private static void StartCooldown(EliteController controller, ZDO zdo)
        {
            float seconds = controller.Rules.PowerOf(Mutation.Devouring, Fields.DevourCooldown);
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            TraitStore.SetDevourReadyAt(zdo, (long)((now + seconds) * 1000.0));
        }
    }
}
