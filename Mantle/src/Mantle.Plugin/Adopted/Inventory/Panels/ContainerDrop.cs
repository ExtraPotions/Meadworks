using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The container panel hangs under the player panel (the game anchors it 30 units below the player panel's bottom
    /// edge). With the wood reaching further past both panels it is lowered by the extra reach of both, so the frames
    /// keep the game's gap; the game's place is remembered the first time and restored with the skin off.
    /// </summary>
    public static class ContainerDrop
    {
        private static RectTransform placed;
        private static float gameY;

        public static void Apply(RectTransform container, float extraReach)
        {
            if (container == null)
                return;
            if (container != placed)
            {
                placed = container;
                gameY = container.anchoredPosition.y;
            }
            float y = gameY - extraReach * 2f;
            if (!Mathf.Approximately(container.anchoredPosition.y, y))
                container.anchoredPosition = new Vector2(container.anchoredPosition.x, y);
        }
    }
}
