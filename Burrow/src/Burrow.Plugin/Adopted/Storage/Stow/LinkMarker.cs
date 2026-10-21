using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burrow.Stow
{
    /// <summary>A thin world-space line from the player to a container and a floating label above the container,
    /// both gone after the lifetime. The label faces the camera and uses the inventory panel's font.</summary>
    public sealed class LinkMarker : MonoBehaviour
    {
        private static readonly Color LinkColour = new Color(0.44f, 0.76f, 1f, 0.9f);
        private static Material lineMaterial;

        private LineRenderer line;
        private TextMeshPro label;
        private Transform target;
        private float endTime;

        public static void Create(Player player, Container container, string text, float seconds)
        {
            GameObject go = new GameObject("Burrow_link");
            LinkMarker marker = go.AddComponent<LinkMarker>();
            marker.target = container.transform;
            marker.endTime = Time.time + Mathf.Max(0.5f, seconds);
            marker.line = BuildLine(go);
            marker.label = BuildLabel(go, text);
            marker.Refresh();
        }

        private void Update()
        {
            if (Time.time >= endTime || target == null || Player.m_localPlayer == null)
            {
                Destroy(gameObject);
                return;
            }
            Refresh();
        }

        private void Refresh()
        {
            Vector3 from = Player.m_localPlayer.transform.position + Vector3.up;
            Vector3 to = target.position + Vector3.up * 0.5f;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            label.transform.position = target.position + Vector3.up * 1.6f;
            Camera camera = Camera.main;
            if (camera != null)
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position);
        }

        private static LineRenderer BuildLine(GameObject go)
        {
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.06f;
            line.endWidth = 0.06f;
            line.material = LineMaterial();
            line.startColor = LinkColour;
            line.endColor = LinkColour;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static TextMeshPro BuildLabel(GameObject parent, string text)
        {
            GameObject go = new GameObject("label");
            go.SetActive(false);   // the text wakes with its font set, so it never looks for TextMeshPro's missing default
            go.transform.SetParent(parent.transform, false);
            TextMeshPro label = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = LabelFont();
            if (font != null)
                label.font = font;
            go.SetActive(true);
            label.text = text;
            label.fontSize = 3.5f;
            label.color = LinkColour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            return label;
        }

        private static TMP_FontAsset LabelFont()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui != null && gui.m_containerName != null && gui.m_containerName.font != null)
                return gui.m_containerName.font;
            return TMP_Settings.defaultFontAsset;
        }

        private static Material LineMaterial()
        {
            if (lineMaterial != null)
                return lineMaterial;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Standard");
            lineMaterial = new Material(shader) { color = LinkColour };
            return lineMaterial;
        }
    }
}
