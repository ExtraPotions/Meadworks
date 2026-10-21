using UnityEngine;

/// <summary>
/// Sits on the creature's animator, as the game's own event receiver does, and notes when an attack animation reaches
/// its OnAttackTrigger event (the frame the bite lands).
/// </summary>
public class PreviewEvents : MonoBehaviour
{
    public static float LastBite = -10f;

    public void OnAttackTrigger()
    {
        LastBite = Time.time;
    }
}
