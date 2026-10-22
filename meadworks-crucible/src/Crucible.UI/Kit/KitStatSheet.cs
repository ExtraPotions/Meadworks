using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A stat sheet in PackPanel's Gear-tab look (<c>GearStats</c>): a dark rounded inset (<see cref="RoundedFill"/>)
    /// holding a scrolling list (<see cref="SheetScroll"/>) of label, value and breakdown-tooltip rows
    /// (<see cref="StatRows"/>). The product fills a <see cref="KitSheet"/>; the rows are drawn again only when its key
    /// changes, and <see cref="Refresh"/> asks for a new sheet at most four times a second (and redraws after two
    /// seconds even when the product's inputs look unchanged).
    /// </summary>
    public sealed class KitStatSheet
    {
        private readonly RectTransform area;
        private readonly RectTransform content;
        private readonly KitSheet sheet = new KitSheet();
        private string shown;
        private float next;
        private float recheck;
        private long shownInputs = long.MinValue;

        private KitStatSheet(RectTransform area, RectTransform content)
        {
            this.area = area;
            this.content = content;
        }

        public RectTransform Area => area;

        /// <summary>The sheet's inset under <paramref name="parent"/>, top-left at <paramref name="at"/>; null headless.</summary>
        public static KitStatSheet Create(RectTransform parent, string name, Vector2 at, Vector2 size)
        {
            if (!CrucibleUiPlugin.Active || parent == null)
                return null;
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform area = (RectTransform)go.transform;
            area.SetParent(parent, false);
            area.anchorMin = area.anchorMax = area.pivot = new Vector2(0f, 1f);
            area.anchoredPosition = at;
            area.sizeDelta = size;
            Image fill = go.GetComponent<Image>();
            fill.sprite = RoundedFill.Sprite;
            fill.type = Image.Type.Sliced;
            fill.color = KitUnity.Colour(KitPalette.SheetInset);
            fill.raycastTarget = false;
            if (KitForged.InWindow(parent))
                KitForged.SheetFill(fill);
            RectTransform content = SheetScroll.Make(area, KitRowLayout.SheetPadding, KitRowLayout.SheetPadding);
            return new KitStatSheet(area, content);
        }

        private float Width => area.sizeDelta.x - KitRowLayout.SheetPadding * 2f;

        /// <summary>Draws <paramref name="fill"/>'s lines now if they differ from what is shown.</summary>
        public void Show(Action<KitSheet> fill)
        {
            sheet.Clear();
            fill?.Invoke(sheet);
            string key = sheet.Key;
            if (key == shown)
                return;
            shown = key;
            KitCanvas.HangulWordBreaks();
            StatRows.Write(content, sheet, Width, KitCanvas.Font);
        }

        /// <summary>
        /// Call every frame while the sheet may show: at most every <see cref="KitRowLayout.RefreshSeconds"/>, and only
        /// when <paramref name="inputs"/> (the product's fingerprint of what the sheet depends on) changed or
        /// <see cref="KitRowLayout.RecheckSeconds"/> passed, the sheet is filled again and redrawn if its lines changed.
        /// </summary>
        public void Refresh(long inputs, Action<KitSheet> fill)
        {
            float now = Time.unscaledTime;
            if (content == null || now < next || !content.gameObject.activeInHierarchy)
                return;
            next = now + KitRowLayout.RefreshSeconds;
            if (inputs == shownInputs && now < recheck)
                return;
            shownInputs = inputs;
            recheck = now + KitRowLayout.RecheckSeconds;
            Show(fill);
        }

        /// <summary>Forget what is shown, so the next <see cref="Refresh"/> redraws (after a resize or language change).</summary>
        public void Invalidate()
        {
            shown = null;
            next = 0f;
            recheck = 0f;
        }
    }

    /// <summary>
    /// A grid of cells in the kit look: copies of the game's inventory cell when a world is loaded (so hover, selection
    /// and icons behave as the game's), else plain images; each skinned (<see cref="KitSkin.Cell"/>), optionally blocked
    /// (<see cref="BlockedCell"/>) and reachable with the gamepad (<see cref="Navigator"/>), which skips blocked cells.
    /// </summary>
    public sealed class KitCellGrid
    {
        /// <summary>Room between plain cells when the game's cell cannot be copied.</summary>
        public const float CellGap = 6f;

        private readonly List<GameObject> cells = new List<GameObject>();

        public int Columns { get; }
        public int Rows { get; }
        public float Step { get; }
        public RectTransform Root { get; }
        public IReadOnlyList<GameObject> Cells => cells;

        private KitCellGrid(RectTransform root, int columns, int rows, float step)
        {
            Root = root;
            Columns = columns;
            Rows = rows;
            Step = step;
        }

        /// <summary>The cell spacing the game's own inventory grid uses, or PackPanel's 70 without one.</summary>
        public static float GameStep => InventoryGui.instance != null && InventoryGui.instance.m_playerGrid != null
            ? InventoryGui.instance.m_playerGrid.m_elementSpace : 70f;

        public static KitCellGrid Create(RectTransform parent, string name, int columns, int rows, Vector2 at, float step = 0f)
        {
            if (!CrucibleUiPlugin.Active || parent == null || columns <= 0 || rows <= 0)
                return null;
            if (step <= 0f)
                step = GameStep;
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = at;
            root.sizeDelta = new Vector2(columns * step, rows * step);
            KitCellGrid grid = new KitCellGrid(root, columns, rows, step);
            GameObject prefab = InventoryGui.instance != null && InventoryGui.instance.m_playerGrid != null ? InventoryGui.instance.m_playerGrid.m_elementPrefab : null;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    grid.cells.Add(grid.Cell(prefab, x, y));
            return grid;
        }

        public GameObject At(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows ? cells[y * Columns + x] : null;

        public void Block(int x, int y, bool blocked) => BlockedCell.Mark(At(x, y), blocked);

        /// <summary>Gamepad navigation over the grid that skips blocked and hidden cells (C07 <see cref="GridNavigator"/>).</summary>
        public GridNavigator Navigator(bool wrap = false) => new GridNavigator(Columns, Rows, c =>
        {
            GameObject cell = At(c.X, c.Y);
            return cell != null && cell.activeInHierarchy && !BlockedCell.IsBlocked(cell);
        }, null, wrap);

        /// <summary>Applies the current look to every cell again (after a theme change).</summary>
        public void Refresh()
        {
            foreach (GameObject cell in cells)
                if (cell != null)
                    KitSkin.Cell(cell.GetComponent<Image>());
        }

        private GameObject Cell(GameObject prefab, int x, int y)
        {
            GameObject cell = prefab != null ? UnityEngine.Object.Instantiate(prefab, Root) : new GameObject("cell", typeof(RectTransform), typeof(Image));
            cell.name = "cell_" + x + "_" + y;
            cell.transform.SetParent(Root, false);
            cell.SetActive(true);
            RectTransform rect = (RectTransform)cell.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * Step + Step / 2f, -(y * Step + Step / 2f));
            if (prefab == null)
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Step - CellGap, Step - CellGap);
            }
            KitSkin.Cell(cell.GetComponent<Image>());
            return cell;
        }
    }
}
