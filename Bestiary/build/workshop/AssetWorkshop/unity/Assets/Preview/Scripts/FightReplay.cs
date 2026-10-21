using UnityEngine;

/// <summary>
/// Replays the choreographed fight on the real creature: the animator gets the same inputs the game will send (the
/// sleeping and dead flags, forward_speed while it hops, the ambush, lunge and stagger triggers), while this script
/// places the mimic and the stand-in player each frame and follows them with the camera. Loops forever.
/// </summary>
public class FightReplay : MonoBehaviour
{
    public TextAsset fightJson;
    public Animator mimic;
    public Transform player;
    public Transform sword;
    public Camera view;

    private const float HopSpeed = 0.66f;
    private static readonly Vector3 CameraOffset = new Vector3(-4.6f, 2.9f, 5.2f);
    private int nextCue;

    public FightData Data { get; private set; }
    public float Frame { get; private set; }

    private void Awake() => Begin();

    private void Update() => Advance(Time.deltaTime);

    private void LateUpdate()
    {
        Place();
        Follow(Time.deltaTime);
    }

    /// <summary>Loads the fight and starts it from the top. Play mode calls it from Awake; tools call it directly.</summary>
    public void Begin()
    {
        Data = JsonUtility.FromJson<FightData>(fightJson.text);
        mimic.applyRootMotion = true;   // keeps the lunge off the root bone; Place() positions the creature
        Restart();
    }

    /// <summary>Moves the fight on by dt seconds and gives the animator any cues that came due.</summary>
    public void Advance(float dt)
    {
        Frame += dt * Data.fps;
        if (Frame > Data.end + 30)
            Restart();
        while (nextCue < Data.cues.Length && Data.cues[nextCue].frame <= Frame)
            Play(Data.cues[nextCue++].clip);
    }

    /// <summary>Puts the mimic, the player and the sword where the fight has them now (after the animator ran).</summary>
    public void Place()
    {
        PlaceMimic();
        PlacePlayer();
        var (i, t) = FightData.Span(Data.sword.Length, k => Data.sword[k].frame, Frame);
        float angle = Mathf.Lerp(Data.sword[i].angle, Data.sword[Mathf.Min(i + 1, Data.sword.Length - 1)].angle, t);
        sword.localRotation = Quaternion.Euler(angle, 0, 0);
    }

    private void Restart()
    {
        Frame = 1f;
        nextCue = 0;
        mimic.Rebind();
        mimic.SetBool("sleeping", true);
    }

    private void Play(string clip)
    {
        mimic.SetFloat("forward_speed", clip == "walk" ? HopSpeed : 0f);
        if (clip == "sleep")
            mimic.SetBool("sleeping", true);
        else if (clip == "ambush" || clip == "lunge")
        {
            mimic.SetBool("sleeping", false);
            mimic.SetTrigger(clip);
        }
        else if (clip == "stagger")
            mimic.SetTrigger("stagger");
        else if (clip == "death")
            mimic.SetBool("dead", true);
    }

    private void PlaceMimic()
    {
        var keys = Data.mimic;
        var (i, t) = FightData.Span(keys.Length, k => keys[k].frame, Frame);
        var a = keys[i];
        var b = keys[Mathf.Min(i + 1, keys.Length - 1)];
        mimic.transform.SetPositionAndRotation(
            new Vector3(Mathf.Lerp(a.x, b.x, t), 0, Mathf.Lerp(a.z, b.z, t)),
            Quaternion.Euler(0, Mathf.LerpAngle(a.yaw, b.yaw, t), 0));
    }

    private void PlacePlayer()
    {
        var keys = Data.player;
        var (i, t) = FightData.Span(keys.Length, k => keys[k].frame, Frame);
        var a = keys[i];
        var b = keys[Mathf.Min(i + 1, keys.Length - 1)];
        player.position = new Vector3(Mathf.Lerp(a.x, b.x, t), 0, Mathf.Lerp(a.z, b.z, t));
        player.rotation = Quaternion.Euler(Mathf.Lerp(a.lean, b.lean, t), Mathf.LerpAngle(a.yaw, b.yaw, t),
            -Mathf.Lerp(a.tilt, b.tilt, t));
        player.localScale = new Vector3(1, Mathf.Lerp(a.crouch, b.crouch, t), 1);
    }

    /// <summary>A camera on a fixed offset from a point between the two, weighted towards the mimic.</summary>
    public void Follow(float dt)
    {
        Vector3 target = Vector3.Lerp(player.position, mimic.transform.position, 0.6f) + Vector3.up * 0.5f;
        float ease = 1f - Mathf.Exp(-3f * dt);
        view.transform.position = Vector3.Lerp(view.transform.position, target + CameraOffset, ease);
        view.transform.LookAt(target);
    }
}
