using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Every client hangs a player's worn backpack on that player's back: the player's ZDO names the pack
    /// (<see cref="Backpack.WornKey"/>, written by the player's own client), and when it changes the model is swapped or
    /// taken down. It hangs on the Spine2 bone, the upper back, the way the game hangs a sheathed shield on the spine: kept
    /// at its own world size, turned upright in the player's frame (every pack's model is built with Spine2's rest
    /// position as its origin, AssetWorkshop's <c>mantle_*</c> assets). Runs after the game's own equipment update, every
    /// frame for every player in view; unless something changed it only reads the ZDO key and whether the cape changed
    /// (<see cref="CapeHold"/> keeps a cape from flapping through the pack), the mount being remembered per character. The main menu's character has no ZDO
    /// and wears none.
    /// </summary>
    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.UpdateEquipmentVisuals))]
    public static class BackpackMount
    {
        private const string Bone = "Spine2";
        private const string Name = "Mantle_backpack";

        /// <summary>Each character's mount, kept as long as the character lives.</summary>
        private static readonly ConditionalWeakTable<VisEquipment, Hanging> Hangings = new ConditionalWeakTable<VisEquipment, Hanging>();

        /// <summary>Spine2 at rest leans back about 5.7 degrees from the player's upright frame (the game's Player prefab).</summary>
        private static readonly Quaternion Upright = Quaternion.Inverse(new Quaternion(-0.0494f, 0f, 0f, 0.9988f));

        [HarmonyPostfix]
        public static void Postfix(VisEquipment __instance)
        {
            if (!Plugin.WorldReady || !__instance.m_isPlayer || __instance.m_isArmorStand || __instance.m_nview == null)
                return;
            ZDO zdo = __instance.m_nview.GetZDO();
            BackpackKind wanted = zdo != null ? BackpackCatalog.ByHash(zdo.GetInt(Backpack.WornKey)) : null;
            if (wanted != null && wanted.Worn == null)
                wanted = null;
            if (!__instance.m_boneMap.TryGetValue(Bone, out Transform spine) || spine == null)
                return;
            Hanging hanging = Hangings.GetOrCreateValue(__instance).On(spine);
            if (hanging.Id == wanted?.Id)
            {
                CapeHold.Follow(hanging.Mount, __instance.m_shoulderItemInstances);
                BackGearClearance.Follow(hanging.Mount, __instance);
                return;
            }
            if (hanging.Mount != null)
                TakeDown(hanging.Mount);
            hanging.Set(wanted != null ? Hang(spine, wanted, __instance.m_shoulderItemInstances) : null, wanted?.Id);
            BackGearClearance.Follow(hanging.Mount, __instance);
            __instance.UpdateLodgroup();   // collects the renderers under the character's visual again
        }

        /// <summary>The prefab name of the pack on a mount (its model is named after it), or null.</summary>
        private static string Wears(Transform mount) => mount != null && mount.childCount > 0 ? mount.GetChild(0).name : null;

        /// <summary>
        /// What hangs on one character's Spine2, remembered between frames so the every-frame check reads no names and looks
        /// up no children: looked up again only on a new bone (a new body) or when the mount went away.
        /// </summary>
        private sealed class Hanging
        {
            private Transform spine;

            public Transform Mount { get; private set; }

            /// <summary>The worn pack's prefab id, null for none.</summary>
            public string Id { get; private set; }

            public Hanging On(Transform bone)
            {
                if (spine != bone || (Id != null && Mount == null))
                {
                    spine = bone;
                    Mount = bone.Find(Name);
                    Id = Wears(Mount);
                }
                return this;
            }

            public void Set(Transform mount, string id)
            {
                Mount = mount;
                Id = mount != null ? id : null;
            }
        }

        /// <summary>
        /// A mount made in the world and parented keeping its world size (as the game attaches its own items), placed on the
        /// bone and turned upright; the model goes under it as the prefab has it, and the cape worn is held under it.
        /// </summary>
        private static Transform Hang(Transform spine, BackpackKind kind, List<GameObject> capes)
        {
            GameObject mount = new GameObject(Name);
            mount.transform.SetParent(spine, true);
            mount.transform.localPosition = Vector3.zero;
            mount.transform.localRotation = Upright;
            Object.Instantiate(kind.Worn, mount.transform, false).name = kind.Id;
            CapeHold.Attach(mount, capes);
            return mount.transform;
        }

        /// <summary>Off the character at once, so the LOD group update right after leaves it out; destroyed at the frame's end.</summary>
        private static void TakeDown(Transform mount)
        {
            CapeHold.Let(mount);
            BackGearClearance.Let(mount);
            mount.SetParent(null, false);
            Object.Destroy(mount.gameObject);
        }
    }
}
