namespace Mantle.Presentation
{
    /// <summary>
    /// A notice's opacity the way the game fades its own messages (<c>MessageHud.ShowMessage</c>): fully visible when
    /// shown, then a straight fade to nothing over <see cref="Seconds"/>.
    /// </summary>
    public static class NoticeFade
    {
        public const float Seconds = 4f;

        public static float Alpha(float shownFor)
        {
            if (float.IsNaN(shownFor) || shownFor <= 0f) return 1f;
            return shownFor >= Seconds ? 0f : 1f - shownFor / Seconds;
        }

        public static bool Over(float shownFor) => shownFor >= Seconds;
    }
}
