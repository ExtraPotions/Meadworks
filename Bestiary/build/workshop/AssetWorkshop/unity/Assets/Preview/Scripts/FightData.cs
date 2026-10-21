using System;
using UnityEngine;

/// <summary>
/// The choreographed fight exported by assets/crypt_mimic/unity_fight.py, in Unity axes: where the mimic stands and
/// faces by frame, the clip cues its animator gets, the player's keys and sword swings, and the HUD timeline.
/// </summary>
[Serializable]
public class FightData
{
    public int fps;
    public int end;
    public MimicKey[] mimic;
    public Cue[] cues;
    public PlayerKey[] player;
    public SwordKey[] sword;
    public HpStep[] mimicHp;
    public HpStep[] playerHp;
    public Caption[] captions;
    public Popup[] popups;
    public Flash[] flashes;
    public Cooldown[] cooldowns;
    public int promptStart;
    public int promptEnd;
    public int reveal;
    public int death;

    /// <summary>Index of the last key at or before frame, and how far (0..1, eased) towards the next one.</summary>
    public static (int index, float t) Span(int count, Func<int, int> frameOf, float frame)
    {
        int i = 0;
        while (i + 1 < count && frameOf(i + 1) <= frame)
            i++;
        if (i + 1 >= count || frame <= frameOf(i))
            return (i, 0f);
        float t = (frame - frameOf(i)) / (frameOf(i + 1) - frameOf(i));
        return (i, Mathf.SmoothStep(0f, 1f, t));
    }
}

[Serializable] public class MimicKey { public int frame; public float x, z, yaw; }
[Serializable] public class Cue { public int frame; public string clip; }
[Serializable] public class PlayerKey { public int frame; public float x, z, yaw, lean, tilt, crouch; }
[Serializable] public class SwordKey { public int frame; public float angle; }
[Serializable] public class HpStep { public int frame, value, max; }
[Serializable] public class Caption { public int frame; public string text; }
[Serializable] public class Popup { public int frame; public string target, text, color; }
[Serializable] public class Flash { public int frame; public string text, color; }
[Serializable] public class Cooldown { public int start, ready; }
