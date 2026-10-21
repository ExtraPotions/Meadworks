// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// <c>wildblood inspect</c>: prints the resolved mark of the creature under the crosshair - its stars, its mutations (or a
    /// boss's aspect), the biome it was rolled in, and the values those produced AFTER star power and any large-star enhancement (not the
    /// configured numbers). Read-only; runs on whichever machine the admin is looking from, reading the creature's
    /// already-synced traits, so it needs no ownership.
    /// </summary>
    public static class InspectCommand
    {
        public static void Run(Terminal.ConsoleEventArgs args)
        {
            Character? target = UnderCrosshair();
            EliteController? controller = target != null ? target.GetComponent<EliteController>() : null;
            if (target == null)
            {
                CommandReply.Send(args, "wildblood inspect: no creature under the crosshair.");
                return;
            }
            if (controller == null || !controller.Ready)
            {
                CommandReply.Send(args, $"wildblood inspect: {target.GetHoverName()} is plain (not yet resolved).");
                return;
            }
            Report(args, controller);
        }

        private static void Report(Terminal.ConsoleEventArgs args, EliteController controller)
        {
            Character c = controller.Creature;
            CreatureTraits t = controller.Traits;
            BiomeRules r = controller.Rules;
            CommandReply.Send(args, $"wildblood inspect: {c.GetHoverName()}");
            CommandReply.Send(args, $"  stars {t.Stars} ({t.LargeGlyphs} large, {t.Stars % 5} small), biome {TraitStore.GetBiome(controller.View.GetZDO())}{(t.Tier > 0 ? $", rolled at world tier {t.Tier}" : "")}{(t.Stars == 0 && c.GetLevel() > 1 ? $", game level {c.GetLevel()} kept" : "")}");
            CommandReply.Send(args, $"  health x{StatMath.HealthMultiplier(r, t) * AspectMath.HealthFactor(t):0.00}, size x{StatMath.SizeMultiplier(r, t) * AspectMath.SizeFactor(t):0.00}, move x{StatMath.MoveMultiplier(r, t, 0f, ExtraPotions.Wildblood.AdoptedCm.CreatureModifierManager.GetSwiftMovementBonus(c)):0.00}, swing x{StatMath.SwingSpeedMultiplier(r, t):0.00}, attack x{DamageMath.OutgoingMultiplier(r, t, 1f):0.00}");
            CommandReply.Send(args, $"  max health {c.GetMaxHealth():0}");
            foreach (Mutation m in t.Active())
            {
                CommandReply.Send(args, "  " + MutationReport.Line(r, t, m));
            }
            HoldingLines(args, controller);
            if (c.IsBoss())
            {
                AspectReport.Lines(controller).ForEach(line => CommandReply.Send(args, "  " + line));
            }
        }

        // What it holds on its ZDO: a thief's pouch, a devourer's meals.
        private static void HoldingLines(Terminal.ConsoleEventArgs args, EliteController controller)
        {
            if (controller.Traits.Has(Mutation.Thieving))
            {
                CommandReply.Send(args, "  " + PouchLine(controller));
            }
            if (controller.Traits.Has(Mutation.Devouring))
            {
                CommandReply.Send(args, "  " + MealLine(controller));
            }
        }

        // The meal list the nameplate icons are drawn from, by prefab name, against the allowance the bite checks.
        private static string MealLine(EliteController controller)
        {
            List<int> meals = MealStore.Load(controller.View.GetZDO());
            string ate = $"devoured {meals.Count} of {DevourLimits.Allowance(controller.Rules, controller.Traits)}";
            if (meals.Count == 0)
            {
                return ate + ": nothing yet";
            }
            List<string> names = new List<string>();
            foreach (int prefabHash in meals)
            {
                GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabHash) : null;
                names.Add(prefab != null ? prefab.name : $"unknown ({prefabHash})");
            }
            return ate + ": " + string.Join(", ", names);
        }

        // The only way to check the icon on the nameplate matches what the ZDO actually holds - the gap between the
        // two is where this feature's bugs live, per the spec. The count is against the same capacity the steal checks
        // (one item per star, at least `max items`), so a full pouch reads as full here too.
        private static string PouchLine(EliteController controller)
        {
            List<PouchStore.Entry> pouch = PouchStore.Load(controller.View.GetZDO());
            string held = $"carrying {pouch.Count} of {PouchStore.ResolvedMaxItems(controller.Rules, controller.Traits)}";
            if (pouch.Count == 0)
            {
                return held + ": nothing";
            }
            List<string> items = new List<string>();
            foreach (PouchStore.Entry entry in pouch)
            {
                items.Add($"{entry.Item.m_shared.m_name} x{entry.Item.m_stack}");
            }
            return held + ": " + string.Join(", ", items);
        }

        // Reads from wherever the admin is looking: a ray straight down the camera, first collider that belongs to a
        // Character. A pure query, so it runs the same on host or client.
        private static Character? UnderCrosshair()
        {
            Camera cam = Camera.main;
            if (cam == null || !Physics.Raycast(cam.transform.position, cam.transform.forward,
                    out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                return null;
            }
            return hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
        }
    }
}
