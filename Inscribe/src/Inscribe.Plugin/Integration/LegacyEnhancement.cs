// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game side of the removed enhancement system's one-time conversion (Inscribe 0.2.0); the plan is
// Inscribe.Core's EnhancementConversion. Spec: .ai/BATCH-NEXT-ITEM-UPGRADES-20261019.md, "Conversion (implemented)".
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Enhancement;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Localization;
using ExtraPotions.Inscribe.Runes;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Keeps the four old scroll prefabs registered (inert, so saves keep them) and converts enhanced items and scrolls in
    /// inventories this machine owns: the local player's when it spawns and after a pickup, a container when it is opened
    /// and this machine owns it (at once, or on the first frame a remote opener's ownership arrives). Each item and each inventory's scrolls convert all at once or not at all: when the
    /// replacement does not fit, nothing changes and the next pass tries again. Steps aside while ItemEnhancements itself is
    /// installed, and runs only while rarity runs (the conversion writes inscriptions).
    /// </summary>
    internal static class LegacyEnhancement
    {
        private const string EssencePrefab = "ECF_Essence";
        private const string ScrollBase = "Amber";

        private static Harmony? harmony;
        private static MeadworksLog? log;
        private static GameObject? holder;
        private static readonly Dictionary<int, GameObject> Scrolls = new Dictionary<int, GameObject>();
        private static float noRoomShownAt = -999f;
        private static bool converting;

        internal static int PatchFailures { get; private set; }

        /// <summary>Items and scroll stacks converted this session (diagnostics and native checks).</summary>
        internal static int ItemsConverted { get; private set; }
        internal static int ScrollStacksConverted { get; private set; }

        internal static bool ScrollPrefabsBuilt => Scrolls.Count == EnhancementConversion.ScrollTiers && Scrolls.Values.All(p => p != null);

        internal static void Start(MeadworksLog value)
        {
            log = value;
            if (InscribeModules.StepAside.SteppedAside(InscribeModule.Enhancement)) return;
            harmony = new Harmony(Plugin.Guid + ".legacy-enhancement");
            try { harmony.CreateClassProcessor(typeof(Patches)).Patch(); }
            catch (Exception error) { PatchFailures++; value.Error("Inscribe enhancement conversion patch refused: " + error.Message); }
            // With EliteCrafting running instead of Inscribe's rarity, still carry the legacy keys through workbench upgrades.
            if (InscribeModules.StepAside.SteppedAside(InscribeModule.Rarity))
                try { harmony.CreateClassProcessor(typeof(UpgradeCarryOver)).Patch(); }
                catch (Exception error) { PatchFailures++; value.Error("Inscribe upgrade carry-over refused: " + error.Message); }
        }

        private static float nextTick;
        private static readonly OpenContainerConversion OpenWatch = new OpenContainerConversion();

        /// <summary>
        /// Converts an open container this machine owns, once per opening. Load first: the inventory in memory reloads only on
        /// the game's once-a-second CheckForChanges, so a chest just handed over can still hold what it had before the last
        /// owner's changes, and saving that would undo them. Legacy runes convert inside that load (LegacyRuneConversion), which
        /// the game does not save; a change is signalled so the owned container writes the successor runes to its ZDO.
        /// </summary>
        private static void ConvertOpened(Container container)
        {
            int runesBefore = LegacyRuneConversion.Converted;
            container.Load();
            bool changed = Convert(container.GetInventory());
            bool unsaved = LegacyRuneConversion.TakeUnsaved(container);
            if (!changed && (unsaved || LegacyRuneConversion.Converted != runesBefore)) container.GetInventory().Changed();
        }

        /// <summary>From Update, every frame: the open container whose ownership arrived after it opened.</summary>
        private static void WatchOpen()
        {
            if (OpenWatch.Waiting == null) return;
            Container? open = InventoryGui.instance != null && InventoryGui.IsVisible() ? InventoryGui.instance.m_currentContainer : null;
            if (OpenWatch.Frame(open != null ? open.GetInstanceID() : (int?)null, open != null && open.IsOwner())) ConvertOpened(open!);
        }

        /// <summary>From Update: the local player's inventory again every few seconds, for a spawn that came before the rules or the server's answer.</summary>
        internal static void Tick()
        {
            if (harmony == null) return;
            WatchOpen();
            if (Player.m_localPlayer == null || Time.time < nextTick) return;
            nextTick = Time.time + 5f;
            var inventory = Player.m_localPlayer.GetInventory();
            if (inventory != null && inventory.GetAllItems().Any(i => EnhancementConversion.ScrollTier(i?.m_dropPrefab?.name) > 0
                || (i?.m_customData != null && EnhancementConversion.NeedsConversion(i.m_customData))))
                Convert(inventory);
        }

        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            OpenWatch.Reset();
            if (holder != null) UnityEngine.Object.Destroy(holder);
            holder = null;
            Scrolls.Clear();
            ItemsConverted = ScrollStacksConverted = 0;
            log = null;
        }

        /// <summary>Converts what this inventory holds; true when anything changed. Public to the native checks through reflection.</summary>
        internal static bool Convert(Inventory? inventory)
        {
            if (inventory == null || converting || ObjectDB.instance == null
                || !EnhancementConversion.MayRun(InscribeModules.Active(InscribeModule.Enhancement), ExtraPotions.Meadworks.Crucible.Plugin.Session.AuthorityReady)) return false;
            converting = true;
            try
            {
                bool changed = ConvertScrolls(inventory);
                foreach (var item in inventory.GetAllItems().ToList())
                    if (item?.m_customData != null && EnhancementConversion.NeedsConversion(item.m_customData))
                        changed |= ConvertItem(inventory, item);
                if (changed) inventory.Changed();
                return changed;
            }
            catch (Exception error)
            {
                log?.Error("Inscribe enhancement conversion stopped: " + error.GetType().Name + ": " + error.Message);
                return false;
            }
            finally { converting = false; }
        }

        private static bool ConvertScrolls(Inventory inventory)
        {
            var stacks = inventory.GetAllItems().Where(i => EnhancementConversion.ScrollTier(i?.m_dropPrefab?.name) > 0).ToList();
            if (stacks.Count == 0) return false;
            var counts = new int[EnhancementConversion.ScrollTiers];
            foreach (var stack in stacks) counts[EnhancementConversion.ScrollTier(stack.m_dropPrefab.name) - 1] += stack.m_stack;
            var plan = EnhancementConversion.PlanScrolls(counts);
            var honing = Prefab(RuneDef.PrefabOf("honing"));
            var essence = Prefab(EssencePrefab);
            if ((plan.HoningRunes > 0 && honing == null) || (plan.Essence > 0 && essence == null)) return false;
            if (!Fits(inventory, stacks.Count, (honing, plan.HoningRunes), (essence, plan.Essence))) { NoRoom(); return false; }
            // Only stacks that actually left the inventory are paid out: another mod can refuse a removal (Burrow's
            // inventory hold does), and a refused stack must stay a scroll for the next pass rather than be paid twice.
            var removed = new int[EnhancementConversion.ScrollTiers];
            int removedStacks = 0;
            if (Player.m_localPlayer == null) return false;
            foreach (var stack in stacks)
            {
                if (!SafeInventory.RemoveStack(inventory, stack)) continue;
                removed[EnhancementConversion.ScrollTier(stack.m_dropPrefab.name) - 1] += stack.m_stack;
                removedStacks++;
            }
            if (removedStacks == 0) return false;
            if (removedStacks != stacks.Count) plan = EnhancementConversion.PlanScrolls(removed);
            // The scrolls are gone now, so their replacement must land somewhere: what the inventory refuses is dropped.
            Deliver(inventory, honing, plan.HoningRunes, false);
            Deliver(inventory, essence, plan.Essence, false);
            ScrollStacksConverted += removedStacks;
            Tell(InscribeText.Format(InscribeTextKeys.ScrollsConverted, plan.HoningRunes, plan.Essence));
            return true;
        }

        private static bool ConvertItem(Inventory inventory, ItemDrop.ItemData item)
        {
            ItemState state = ItemState.Read(item);
            if (state.IsNewerFormat) return false;
            var conversion = EnhancementConversion.PlanItem(EnhancementConversion.ReadLevel(item.m_customData), Candidates(item, state));
            var honing = conversion.HoningRunes > 0 ? Prefab(RuneDef.PrefabOf("honing")) : null;
            if (conversion.HoningRunes > 0 && honing == null) return false;
            if (!Fits(inventory, 0, (honing, conversion.HoningRunes), (null, 0))) { NoRoom(); return false; }
            if (conversion.HoningRunes > 0 && Player.m_localPlayer == null) return false;
            if (conversion.Raised.Count > 0)
            {
                ItemStateBuilder builder = state.ToBuilder();
                foreach (var raised in conversion.Raised)
                {
                    string id = state.Affixes[raised.Index].Id;
                    if (!builder.ReplaceAffix(id, new AffixRoll(id, raised.Grade, raised.Value))) return false;
                }
                if (!ItemState.Write(item, builder.Build())) return false;
            }
            // Marked only once the runes are confirmed: in the inventory, or on the ground when it took only some. When it
            // took none (another mod holds it), the raised tiers are put back and the next pass tries again.
            if (!Deliver(inventory, honing, conversion.HoningRunes, true))
            {
                if (conversion.Raised.Count > 0) ItemState.Write(item, state);
                NoRoom();
                return false;
            }
            EnhancementConversion.Mark(item.m_customData, conversion);
            ItemsConverted++;
            Tell(InscribeText.Format(InscribeTextKeys.EnhancementConverted, global::Localization.instance?.Localize(item.m_shared.m_name) ?? item.m_shared.m_name,
                conversion.Level, conversion.TiersRaised, conversion.HoningRunes));
            return true;
        }

        /// <summary>Each inscription with the stronger rows the Honing Rune's rules open on this item; null where none can rise.</summary>
        private static List<ConvertibleInscription?> Candidates(ItemDrop.ItemData item, ItemState state)
        {
            var list = new List<ConvertibleInscription?>();
            if (state.IsSealed || state.IsUnknownRarity) return list;
            RuleSet rules = ActiveRules.Current;
            ClassInfo cls = ItemClasses.ClassifyForRune(item);
            int itemLevel = ItemTier.Of(item);
            for (int i = 0; i < state.AffixCount; i++)
            {
                AffixDef? def = state.DefinitionAt(i);
                AffixRoll roll = state.Affixes[i];
                AffixTierDef? row = def?.TierRow(roll.Tier);
                if (def == null || !def.Enabled || def.Value == AffixValueType.Flag || row == null) { list.Add(null); continue; }
                ClassFit fit = def.FitFor(cls.ClassId);
                int closed = TierEligibility.ClosedTiers(def, fit == ClassFit.Best ? ClassFit.Best : ClassFit.Allowed, rules.Economy.Rolling.AllowedClosedFraction);
                float scale = def.Scaled ? cls.DamageScale : 1f;
                var higher = new List<TierRange>();
                for (int grade = roll.Tier + 1; ; grade++)
                {
                    AffixTierDef? next = def.TierRow(grade);
                    if (next == null || !TargetedRules.CanHone(true, next.Weight, next.Level, next.Shown, itemLevel, closed)) break;
                    higher.Add(Range(next, scale));
                }
                list.Add(new ConvertibleInscription(Range(row, scale), roll.Value, higher));
            }
            return list;
        }

        private static TierRange Range(AffixTierDef row, float scale) =>
            new TierRange(row.Grade, RollMath.Scale(row.Min, scale, row.Decimals), RollMath.Scale(row.Max, scale, row.Decimals), row.Decimals);

        private static GameObject? Prefab(string name) => ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;

        /// <summary>Whether both outputs fit, counting the slots <paramref name="freed"/> stacks leave behind.</summary>
        private static bool Fits(Inventory inventory, int freed, (GameObject? Prefab, int Count) a, (GameObject? Prefab, int Count) b)
        {
            int slots = inventory.GetEmptySlots() + freed;
            foreach (var (prefab, count) in new[] { a, b })
            {
                if (count <= 0 || prefab == null) continue;
                var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
                int max = Math.Max(1, shared.m_maxStackSize);
                int left = Math.Max(0, count - inventory.FindFreeStackSpace(shared.m_name, Game.m_worldLevel));
                slots -= (left + max - 1) / max;
            }
            return slots >= 0;
        }

        /// <summary>
        /// Puts <paramref name="count"/> of the prefab into the inventory (<see cref="SafeInventory.Deliver"/>): what does
        /// not arrive is dropped at the local player's feet with a message. The space check can pass and the add still fall
        /// short: AddItem stops part-way when FindEmptySlot finds no cell (inventory layout mods answer it for fewer cells
        /// than GetEmptySlots counts) and is skipped outright while another mod holds the inventory. False only when
        /// <paramref name="noneMeansRetry"/> is set and nothing arrived: nothing was added or dropped, so the caller retries.
        /// </summary>
        private static bool Deliver(Inventory inventory, GameObject? prefab, int count, bool noneMeansRetry)
        {
            if (prefab == null || count <= 0) return true;
            var player = Player.m_localPlayer;
            var result = SafeInventory.Deliver(inventory, prefab, count, player != null ? SafeInventory.AtFeet(player) : (Vector3?)null, noneMeansRetry);
            if (noneMeansRetry && result.Arrived == 0 && result.Dropped == 0) return false;
            if (result.Dropped > 0)
            {
                log?.Warning("Inscribe enhancement conversion: " + result.Dropped + " " + prefab.name + " did not fit and were dropped at the player.");
                Tell(InscribeText.Text(InscribeTextKeys.ConversionDropped));
            }
            if (result.Missing > 0)
                log?.Error("Inscribe enhancement conversion: " + result.Missing + " " + prefab.name + " did not fit and no player was present to drop them.");
            return true;
        }

        private static void NoRoom()
        {
            if (Time.time - noRoomShownAt < 30f) return;
            noRoomShownAt = Time.time;
            Tell(InscribeText.Text(InscribeTextKeys.ConversionNoRoom));
        }

        private static void Tell(string text)
        {
            if (log != null) InscribeText.Centre(text, log);
        }

        // ---- the inert old scrolls ----

        private static void Register(List<GameObject>? list, Action? changed)
        {
            if (list == null || !InscribeModules.StepAside.ScrollPrefabs) return;
            Build(list);
            bool added = false;
            foreach (var prefab in Scrolls.Values)
                if (prefab != null && !list.Any(p => p != null && p.name == prefab.name)) { list.Add(prefab); added = true; }
            if (added) changed?.Invoke();
        }

        private static void Build(List<GameObject> list)
        {
            if (ScrollPrefabsBuilt) return;
            var baseItem = list.Find(p => p != null && p.name == ScrollBase && p.GetComponent<ItemDrop>() != null);
            if (baseItem == null) return;
            if (holder == null)
            {
                holder = new GameObject("Inscribe_LegacyScrolls");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }
            for (int tier = 1; tier <= EnhancementConversion.ScrollTiers; tier++)
            {
                if (Scrolls.TryGetValue(tier, out var made) && made != null) continue;
                var copy = UnityEngine.Object.Instantiate(baseItem, holder.transform, false);
                copy.name = EnhancementConversion.ScrollPrefabs[tier - 1];
                var drop = copy.GetComponent<ItemDrop>();
                var shared = drop.m_itemData.m_shared;
                shared.m_name = "$" + InscribeCore.TokenPrefix + InscribeTextKeys.ScrollName(tier);
                shared.m_description = "$" + InscribeCore.TokenPrefix + InscribeTextKeys.ScrollDescription(tier);
                shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
                shared.m_maxStackSize = 50;
                shared.m_weight = 0.1f;
                shared.m_value = 0;
                Sprite? icon = Plugin.AllowsUi ? ExtraPotions.Meadworks.OriginalArt.OriginalItemArt.Icon(copy.name) : null;
                if (icon != null) shared.m_icons = new[] { icon };
                drop.m_itemData.m_dropPrefab = copy;
                Scrolls[tier] = copy;
            }
        }

        [HarmonyPatch]
        private static class Patches
        {
            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void DbAwake(ObjectDB __instance) => Register(__instance.m_items, __instance.UpdateRegisters);

            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
            private static void DbCopy(ObjectDB __instance) => Register(__instance.m_items, __instance.UpdateRegisters);

            // Appended before Awake runs, so the game's own loop registers them.
            [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake)), HarmonyPrefix]
            private static void SceneAwake(ZNetScene __instance)
            {
                if (ObjectDB.instance != null) Build(ObjectDB.instance.m_items);
                Build(__instance.m_prefabs);
                Register(__instance.m_prefabs, null);
            }

            [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned)), HarmonyPostfix]
            private static void Spawned(Player __instance)
            {
                if (__instance == Player.m_localPlayer) Convert(__instance.GetInventory());
            }

            [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup)), HarmonyPostfix]
            private static void Pickup(Humanoid __instance, bool __result)
            {
                if (__result && __instance == Player.m_localPlayer) Convert(__instance.GetInventory());
            }

            // The opener owns the container while it is open; the conversion's Changed() saves it to the ZDO. The inventory
            // in memory reloads only on the game's once-a-second CheckForChanges, so a chest just handed over can still hold
            // what it had before the last owner's changes; saving that would undo them (items taken out come back, items put
            // in vanish). Load first: with ownership here, the ZDO already carries the last owner's data.
            // A remote opener receives ownership after the window opens; OpenWatch converts on the first frame it arrives.
            [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show)), HarmonyPostfix]
            private static void Opened(Container container)
            {
                if (container == null) return;
                if (OpenWatch.Opened(container.GetInstanceID(), container.IsOwner())) ConvertOpened(container);
            }
        }
    }
}
