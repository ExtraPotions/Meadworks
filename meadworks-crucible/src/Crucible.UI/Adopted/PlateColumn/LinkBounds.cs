// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/LinkBounds.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using TMPro;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Where a linked word sits on screen, for <see cref="LinkTips"/> to put its tip beside it: the world corners of the
    /// link's characters on its first line (a link the text wrapped is measured to the end of that line), from the
    /// character data TextMeshPro keeps after laying the text out.
    /// </summary>
    internal static class LinkBounds
    {
        /// <summary>Bottom-left, top-left, top-right, bottom-right of the link with this id; null when the text has no such link.</summary>
        public static Vector3[]? Of(TMP_Text text, string id)
        {
            TMP_TextInfo info = text.textInfo;
            for (int i = 0; i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (link.GetLinkID() == id)
                {
                    return Corners(text, link);
                }
            }
            return null;
        }

        private static Vector3[]? Corners(TMP_Text text, TMP_LinkInfo link)
        {
            TMP_TextInfo info = text.textInfo;
            int first = link.linkTextfirstCharacterIndex;
            if (link.linkTextLength <= 0 || first < 0 || first >= info.characterCount)
            {
                return null;
            }
            TMP_CharacterInfo start = info.characterInfo[first];
            Vector3 min = start.bottomLeft;
            Vector3 max = start.topRight;
            int end = Mathf.Min(first + link.linkTextLength, info.characterCount);
            for (int i = first + 1; i < end && info.characterInfo[i].lineNumber == start.lineNumber; i++)
            {
                min = Vector3.Min(min, info.characterInfo[i].bottomLeft);
                max = Vector3.Max(max, info.characterInfo[i].topRight);
            }
            Transform local = text.transform;
            return new[]
            {
                local.TransformPoint(new Vector3(min.x, min.y)), local.TransformPoint(new Vector3(min.x, max.y)),
                local.TransformPoint(new Vector3(max.x, max.y)), local.TransformPoint(new Vector3(max.x, min.y)),
            };
        }
    }
}
