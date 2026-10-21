using UnityEngine;

/// <summary>
/// The Valheim-style HUD over the replayed fight, the same as the video's: the chest's hover prompt while disguised,
/// then a name and health bar over its lid with the bite cooldown beneath; the player's health bottom left; damage
/// numbers, call-outs and captions. Fonts are the game's own when the preview builder found them.
/// </summary>
public class FightHud : MonoBehaviour
{
    public FightReplay replay;
    public Font nameFont;
    public Font textFont;

    private GUIStyle name, big, text, small, number;

    private void OnGUI()
    {
        if (replay == null || replay.Data == null)
            return;
        Styles();
        var data = replay.Data;
        int frame = Mathf.FloorToInt(replay.Frame);
        Prompt(data, frame);
        Nameplate(data, frame);
        PlayerHealth(data, frame);
        Popups(data, frame);
        Flashes(data, frame);
        CaptionBox(data, frame);
    }

    private void Styles()
    {
        if (name != null)
            return;
        name = Style(nameFont, 30);
        big = Style(nameFont, 88);
        text = Style(textFont, 22);
        small = Style(textFont, 16);
        number = Style(textFont, 30);
    }

    private static GUIStyle Style(Font font, int size) =>
        new GUIStyle { font = font, fontSize = size, alignment = TextAnchor.MiddleCenter, richText = false };

    private Vector2 Screen(Transform who, float height)
    {
        Vector3 point = replay.view.WorldToScreenPoint(who.position + Vector3.up * height);
        return new Vector2(point.x, UnityEngine.Screen.height - point.y);
    }

    private void Prompt(FightData data, int frame)
    {
        if (frame < data.promptStart || frame > data.promptEnd)
            return;
        Vector2 at = Screen(replay.mimic.transform, 1.0f);
        Label(at + new Vector2(0, -20), "Stone chest", text, Color.white, 1);
        Label(at + new Vector2(-34, 8), "[E]", text, new Color(1f, 0.8f, 0.35f), 1);
        Label(at + new Vector2(16, 8), "Open", text, Color.white, 1);
    }

    private void Nameplate(FightData data, int frame)
    {
        if (frame < data.reveal || frame > data.death + 30)
            return;
        float alpha = frame < data.death ? 1f : 1f - (frame - data.death) / 30f;
        Vector2 at = Screen(replay.mimic.transform, 1.0f);
        Label(at + new Vector2(0, -34), "Crypt Mimic", name, Color.white, alpha);
        var (hp, max) = Step(data.mimicHp, frame);
        Bar(new Rect(at.x - 95, at.y - 14, 190, 10), (float)hp / max, new Color(0.78f, 0.16f, 0.14f), alpha);
        if (frame >= data.death)
            return;
        var (state, fraction) = CooldownAt(data, frame);
        if (state == null)
            return;
        var colour = state == "ready" ? new Color(1f, 0.67f, 0.16f) : new Color(0.59f, 0.47f, 0.31f);
        Bar(new Rect(at.x - 95, at.y, 190, 5), fraction, colour, alpha);
        Label(at + new Vector2(0, 16), state == "ready" ? "bite ready" : "bite recharging", small, colour, alpha);
    }

    private void PlayerHealth(FightData data, int frame)
    {
        var (hp, max) = Step(data.playerHp, frame);
        float bottom = UnityEngine.Screen.height;
        Label(new Vector2(70, bottom - 70), "Health", small, Color.white, 1);
        Bar(new Rect(40, bottom - 56, 260, 18), (float)hp / max, new Color(0.75f, 0.14f, 0.12f), 1);
        Label(new Vector2(330, bottom - 47), hp.ToString(), text, Color.white, 1);
    }

    private void Popups(FightData data, int frame)
    {
        foreach (var popup in data.popups)
        {
            int age = frame - popup.frame;
            if (age < 0 || age >= 26)
                continue;
            Transform who = popup.target == "mimic" ? replay.mimic.transform : replay.player;
            Vector2 at = Screen(who, popup.target == "mimic" ? 1.0f : 2.05f) + new Vector2(40, -20 - age * 2.2f);
            Label(at, popup.text, number, Colour(popup.color), Mathf.Clamp01((26 - age) / 10f));
        }
    }

    private void Flashes(FightData data, int frame)
    {
        foreach (var flash in data.flashes)
        {
            int age = frame - flash.frame;
            if (age >= 0 && age < 22)
                Label(new Vector2(UnityEngine.Screen.width / 2f, 150 - Mathf.Min(age, 4) * 3), flash.text, big,
                    Colour(flash.color), Mathf.Clamp01((22 - age) / 8f));
        }
    }

    private void CaptionBox(FightData data, int frame)
    {
        string caption = "";
        foreach (var c in data.captions)
            if (c.frame <= frame)
                caption = c.text;
        if (caption.Length == 0)
            return;
        float width = text.CalcSize(new GUIContent(caption)).x + 48;
        var box = new Rect(UnityEngine.Screen.width / 2f - width / 2, UnityEngine.Screen.height - 132, width, 44);
        GUI.color = new Color(0.04f, 0.05f, 0.06f, 0.67f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;   // GUI.color tints everything drawn after it, text included
        Label(box.center, caption, text, Color.white, 1);
    }

    private static (int, int) Step(HpStep[] steps, int frame)
    {
        var current = steps[0];
        foreach (var s in steps)
            if (s.frame <= frame)
                current = s;
        return (current.value, current.max);
    }

    private static (string, float) CooldownAt(FightData data, int frame)
    {
        Cooldown current = null;
        foreach (var c in data.cooldowns)
            if (c.start <= frame)
                current = c;
        if (current == null)
            return (null, 0f);
        return frame < current.ready
            ? ("recharging", (frame - current.start) / (float)(current.ready - current.start))
            : ("ready", 1f);
    }

    private static Color Colour(string name) => name switch
    {
        "red" => new Color(0.9f, 0.24f, 0.2f),
        "blue" => new Color(0.55f, 0.78f, 1f),
        "gold" => new Color(1f, 0.8f, 0.35f),
        _ => Color.white,
    };

    private static void Bar(Rect box, float fraction, Color colour, float alpha)
    {
        GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.8f * alpha);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(colour.r, colour.g, colour.b, alpha);
        var fill = new Rect(box.x + 2, box.y + 2, (box.width - 4) * Mathf.Clamp01(fraction), box.height - 4);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    /// <summary>Centred text with a dark outline, as the game draws its world text.</summary>
    private static void Label(Vector2 centre, string content, GUIStyle style, Color colour, float alpha)
    {
        var size = style.CalcSize(new GUIContent(content));
        var rect = new Rect(centre.x - size.x / 2, centre.y - size.y / 2, size.x, size.y);
        style.normal.textColor = new Color(0, 0, 0, alpha);
        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
            GUI.Label(new Rect(rect.position + offset, rect.size), content, style);
        style.normal.textColor = new Color(colour.r, colour.g, colour.b, alpha);
        GUI.Label(rect, content, style);
    }
}
