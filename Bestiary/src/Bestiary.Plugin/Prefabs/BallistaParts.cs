// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Prefabs
{
    /// <summary>
    /// The moving parts of a Bone Ballista, which wears the game ballista's (`piece_turret`) own model: the neck that
    /// turns it about Y, the body that tilts it, the eye it shoots from, the armed and unarmed bodies (string drawn or
    /// slack) and the missile shown in the groove. Read from the game's Turret once when the piece is built
    /// (Native.Ballista.BallistaPiece), kept as paths from the piece's root, found again on each placed ballista.
    /// </summary>
    public sealed class BallistaParts
    {
        internal static string? BodyPath, NeckPath, EyePath, ArmedPath, UnarmedPath, LaidPath;

        public Transform Body = null!, Neck = null!, Eye = null!;
        public GameObject? Armed, Unarmed, Laid;
        public Quaternion BaseBody, BaseNeck;

        /// <summary>The parts of a placed ballista, or null when its model lacks the turning parts.</summary>
        public static BallistaParts? Of(Transform root)
        {
            Transform? body = Find(root, BodyPath), neck = Find(root, NeckPath), eye = Find(root, EyePath);
            if (body == null || neck == null || eye == null)
                return null;
            return new BallistaParts
            {
                Body = body, Neck = neck, Eye = eye,
                Armed = Find(root, ArmedPath)?.gameObject, Unarmed = Find(root, UnarmedPath)?.gameObject, Laid = Find(root, LaidPath)?.gameObject,
                BaseBody = body.localRotation, BaseNeck = neck.localRotation,
            };
        }

        /// <summary>The path of `part` below `root` ("" for the root itself), or null when it is not below it.</summary>
        internal static string? PathOf(Transform root, Transform? part)
        {
            if (part == null)
                return null;
            string path = "";
            for (Transform t = part; t != root; t = t.parent)
            {
                if (t == null)
                    return null;
                path = path.Length == 0 ? t.name : t.name + "/" + path;
            }
            return path;
        }

        private static Transform? Find(Transform root, string? path) => path == null ? null : path.Length == 0 ? root : root.Find(path);

        /// <summary>
        /// Points the model where the ZDO says, the game turret's own way (Turret.UpdateTurretRotation): the body takes
        /// the whole aim, the neck its turn about Y only.
        /// </summary>
        public void Point(Transform piece, float yaw, float pitch)
        {
            Quaternion aim = Quaternion.Euler(-pitch, piece.eulerAngles.y + yaw, 0f);
            Body.rotation = BaseBody * aim;
            Neck.rotation = BaseNeck * Quaternion.Euler(0f, Body.rotation.eulerAngles.y, Body.rotation.eulerAngles.z);
        }
    }
}
