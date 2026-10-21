using TMPro;
using UnityEngine;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
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
