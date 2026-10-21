// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/FeederPrefab.cs,
// FeederRecipe.cs and FeederGate.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: three trough pieces from the Shepherd catalog; grindstone_feeder alias; recipes from
// Crucible-bound settings re-applied on change; Build Level gate (Q6, 0 = everyone) through the portable TroughGate.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.OriginalArt;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using ExtraPotions.Shepherd.Troughs;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    /// <summary>
    /// The trough pieces (design 11.1): copies of vanilla containers made once per process on every machine, dedicated
    /// server included, whether feeding is on or not, so troughs already built always load. Each copy lives under an
    /// inactive holder kept with DontDestroyOnLoad, so none of its Awakes run and it never gets a ZDO.
    /// <list type="bullet">
    /// <item>After ZNetScene.Awake each prefab joins the scene's prefab list and name table. The GrindstoneSkills Animal
    /// Feeder name <c>grindstone_feeder</c> maps to the Feed Barrel when GrindstoneSkills is not installed, so its saved
    /// feeders load as Feed Barrels and keep their contents (same 4 x 2 inventory) and their saved prefab.</item>
    /// <item>Once the scene and the item database exist, the recipes are applied and the pieces join the Hammer.</item>
    /// <item>Original hollow shells replace source visuals and colliders before the placed instance awakens.</item>
    /// </list>
    /// </summary>
    public static class TroughPieces
    {
        public const string HammerItem = "Hammer";
        public const string GrindstoneGuid = "com.GrindstoneSkills";

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, string> AppliedRecipes = new Dictionary<string, string>();
        private static readonly HashSet<int> Hashes = new HashSet<int>();
        private static GameObject? holder;

        static TroughPieces()
        {
            foreach (TroughPiece piece in TroughCatalog.Pieces) Hashes.Add(piece.Prefab.GetStableHashCode());
            Hashes.Add(TroughPrefabs.GrindstoneFeeder.GetStableHashCode());
        }

        /// <summary>A ZDO prefab hash of a trough (alias included).</summary>
        public static bool IsTroughHash(int prefabHash) => Hashes.Contains(prefabHash);

        /// <summary>A placed container whose ZDO is a trough.</summary>
        public static bool IsTrough(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null!;
            return view != null && view.IsValid() && IsTroughHash(view.GetZDO().GetPrefab());
        }

        public static bool IsTroughPiece(Piece piece)
        {
            if (piece == null) return false;
            foreach (GameObject prefab in Prefabs.Values)
                if (prefab != null && piece == prefab.GetComponent<Piece>()) return true;
            return false;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        private static class SceneAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => NativeFeeding.Run("trough pieces", Install);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class DatabaseAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => NativeFeeding.Run("trough pieces", Install);
        }

        public static void Install()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return;
            var fallbacks = new List<string>();
            foreach (TroughPiece piece in TroughCatalog.Pieces)
                if (!Prefabs.ContainsKey(piece.Prefab)) Build(scene, piece, fallbacks);
            // One line for the whole install, not one per trough.
            if (fallbacks.Count > 0)
                NativeFeeding.Warn("A vanilla chest is missing or changed by another mod; " + string.Join("; ", fallbacks)
                    + ". Saved troughs and their food still load.");
            Register(scene);
            if (ObjectDB.instance == null) return;
            ApplyRecipes(force: true);
            AddToHammer(ObjectDB.instance);
        }

        private static void Build(ZNetScene scene, TroughPiece definition, List<string> fallbacks)
        {
            // Saved troughs are deleted on load when their prefab is not registered, so a trough is always built: from
            // its catalog source, another vanilla chest, or a minimal container of its own. Its prefab name never changes.
            string? sourceName = TroughSource.Choose(definition.SourcePrefab, name => Usable(scene.GetPrefab(name)));
            GameObject copy = sourceName != null ? Object.Instantiate(scene.GetPrefab(sourceName), Holder.transform, false) : SelfMade();
            if (sourceName != definition.SourcePrefab)
                fallbacks.Add("the " + definition.DisplayName + " (normally from " + definition.SourcePrefab + ") is built from "
                    + (sourceName ?? "a minimal container"));
            copy.name = definition.Prefab;
            Piece piece = copy.GetComponent<Piece>();
            Container container = copy.GetComponentInChildren<Container>(true);
            piece.m_name = definition.NameToken;
            piece.m_description = definition.DescriptionToken;
            piece.m_category = Piece.PieceCategory.Furniture;
            piece.m_enabled = true;
            piece.m_craftingStation = Station(scene, definition.Station);
            container.m_name = definition.NameToken;
            container.m_width = definition.Width;
            container.m_height = definition.Height;
            container.m_privacy = Container.PrivacySetting.Public;
            if (definition.Prefab == TroughPrefabs.StoneTrough)
            {
                WearNTear wear = copy.GetComponent<WearNTear>();
                if (wear != null) wear.m_materialType = WearNTear.MaterialType.Stone;
            }
            // Registration must not depend on the art: without it the trough keeps its source look.
            if (!ExtraPotions.Meadworks.Crucible.MeadworksHeadless.IsHeadless)
            {
                try
                {
                    Sprite icon = OriginalItemArt.Icon(definition.Prefab);
                    Reskin(copy, scene, definition.Prefab);
                    piece.m_icon = icon;
                }
                catch (System.Exception error) { NativeFeeding.Warn("The " + definition.DisplayName + " art did not load (" + error.Message + "); it keeps a plain look."); }
            }
            Prefabs[definition.Prefab] = copy;
        }

        /// <summary>A source a trough can be cloned from: a networked piece with a container.</summary>
        internal static bool Usable(GameObject source) =>
            source != null && source.GetComponent<ZNetView>() != null && source.GetComponent<Piece>() != null
            && source.GetComponentInChildren<Container>(true) != null;

        /// <summary>The last resort when no vanilla chest is usable: a persistent networked piece with a container.</summary>
        private static GameObject SelfMade()
        {
            var made = new GameObject("shepherd_trough_minimal");
            made.transform.SetParent(Holder.transform, false);
            int layer = LayerMask.NameToLayer("piece");
            if (layer >= 0) made.layer = layer;
            ZNetView view = made.AddComponent<ZNetView>();
            view.m_persistent = true;
            made.AddComponent<Piece>();
            made.AddComponent<Container>();
            return made;
        }

        private static void Reskin(GameObject copy, ZNetScene scene, string prefab)
        {
            Renderer[] originalRenderers = copy.GetComponentsInChildren<Renderer>(true);
            Collider[] originalColliders = copy.GetComponentsInChildren<Collider>(true);
            int layer = copy.layer;
            foreach (Collider collider in originalColliders)
                if (!collider.isTrigger) { layer = collider.gameObject.layer; break; }
            GameObject visual = OriginalItemArt.Instantiate(scene, prefab, copy.transform);
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            foreach (LODGroup group in copy.GetComponentsInChildren<LODGroup>(true))
                if (!group.transform.IsChildOf(visual.transform)) group.enabled = false;
            foreach (Renderer renderer in originalRenderers) renderer.enabled = false;
            foreach (Collider collider in originalColliders)
            {
                collider.enabled = false;
                // The private prefab must lose solid chest colliders before a same-frame placement can clone it.
                Object.DestroyImmediate(collider);
            }
        }

        private static CraftingStation Station(ZNetScene scene, string prefab)
        {
            GameObject station = scene.GetPrefab(prefab);
            return station != null ? station.GetComponent<CraftingStation>() : null!;
        }

        private static void Register(ZNetScene scene)
        {
            foreach (GameObject prefab in Prefabs.Values)
            {
                if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);
                scene.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
            }
            int alias = TroughPrefabs.GrindstoneFeeder.GetStableHashCode();
            if (!Chainloader.PluginInfos.ContainsKey(GrindstoneGuid) && !scene.m_namedPrefabs.ContainsKey(alias)
                && Prefabs.TryGetValue(TroughPrefabs.FeedBarrel, out GameObject barrel))
                scene.m_namedPrefabs[alias] = barrel;
        }

        /// <summary>Writes each recipe into its prefab when its effective (server) value changed.</summary>
        public static void ApplyRecipes(bool force = false)
        {
            foreach (TroughPiece definition in TroughCatalog.Pieces)
            {
                if (!Prefabs.TryGetValue(definition.Prefab, out GameObject prefab) || prefab == null) continue;
                string text = RecipeText(definition);
                if (!force && AppliedRecipes.TryGetValue(definition.Prefab, out string applied) && applied == text) continue;
                prefab.GetComponent<Piece>().m_resources = Requirements(text);
                AppliedRecipes[definition.Prefab] = text;
            }
        }

        private static string RecipeText(TroughPiece definition)
        {
            if (definition.Prefab == TroughPrefabs.FeedBarrel) return HusbandryCompanionSettings.FeederRecipe?.Value ?? definition.DefaultRecipe;
            if (definition.Prefab == TroughPrefabs.PlankTrough) return ShepherdSettings.PlankRecipe?.Value ?? definition.DefaultRecipe;
            return ShepherdSettings.StoneRecipe?.Value ?? definition.DefaultRecipe;
        }

        private static Piece.Requirement[] Requirements(string text)
        {
            var requirements = new List<Piece.Requirement>();
            foreach (RecipeEntry entry in TroughRecipe.Parse(text, name => Item(name) != null))
            {
                ItemDrop item = Item(entry.Item);
                if (item != null)
                    requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = entry.Amount, m_amountPerLevel = 0, m_recover = true });
            }
            return requirements.ToArray();
        }

        private static ItemDrop Item(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null!;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null!;
            if (prefab == null && ZNetScene.instance != null) prefab = ZNetScene.instance.GetPrefab(prefabName);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null!;
        }

        private static void AddToHammer(ObjectDB database)
        {
            GameObject hammer = database.GetItemPrefab(HammerItem);
            ItemDrop item = hammer != null ? hammer.GetComponent<ItemDrop>() : null!;
            PieceTable table = item != null ? item.m_itemData.m_shared.m_buildPieces : null!;
            if (table == null) return;
            foreach (GameObject prefab in Prefabs.Values)
                if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
        }

        private static GameObject Holder
        {
            get
            {
                if (holder != null) return holder;
                holder = new GameObject("Shepherd_Prefabs");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
                return holder;
            }
        }

        /// <summary>Build Level (design 4.3, Q6): 0 lets everyone, above 100 hides troughs; built troughs keep working.</summary>
        public static bool MayBuild(Player player)
        {
            if (!NativeFeeding.ProductActive) return false;
            float level = HusbandryCompanionSettings.FeederLevel?.Value ?? 0f;
            float keeper = player != null && HusbandrySkill.Active ? HusbandrySkill.Of(player) : 0f;
            return TroughGate.MayBuild(level, keeper, HusbandrySkill.Active);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
        private static class Requirements_Gate
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance, Piece piece, ref bool __result)
            {
                if (__result && IsTroughPiece(piece) && !NativeFeeding.Run("trough gate", static p => MayBuild(p), __instance, true))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
        private static class Menu_Gate
        {
            [HarmonyPostfix]
            private static void Postfix(PieceTable __instance, Player player)
            {
                if (NativeFeeding.Run("trough gate", static p => MayBuild(p), player, true)) return;
                foreach (GameObject prefab in Prefabs.Values)
                {
                    Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null!;
                    if (piece == null) continue;
                    __instance.m_availablePieces.Remove(piece);
                    foreach (List<Piece> category in __instance.m_availablePiecesByCategory) category.Remove(piece);
                }
            }
        }
    }
}
