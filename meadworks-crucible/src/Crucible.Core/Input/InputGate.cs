namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// What is open this frame. The plugin adapter fills it from the game (chat, console, sign text input, a selected
    /// Unity input field, registered mod windows, pause menu, inventory and other game UI). Sources mirror the adopted
    /// OpenKeep <c>Typing</c> and <c>GameWindow</c> helpers and Wildroot's blocked-input rule.
    /// </summary>
    public sealed class InputFacts {
        public bool ChatFocused { get; set; }
        public bool ConsoleVisible { get; set; }
        public bool TextInputVisible { get; set; }
        public bool InputFieldSelected { get; set; }
        /// <summary>A mod window with its own text editing (IMGUI) is open.</summary>
        public bool TypingWindowOpen { get; set; }
        /// <summary>A mod window treated like the game's own windows is open.</summary>
        public bool ModWindowOpen { get; set; }
        public bool MenuVisible { get; set; }
        public bool InventoryVisible { get; set; }
        /// <summary>Other game UI that owns the cursor: store, large map, crafting, text dialogs.</summary>
        public bool OtherGameUiVisible { get; set; }
        public bool Typing=>ChatFocused||ConsoleVisible||TextInputVisible||InputFieldSelected||TypingWindowOpen;
    }

    /// <summary>Strongest reason input is blocked, in priority order.</summary>
    public enum InputBlock { None, Typing, Menu, ModWindow, GameUi }

    /// <summary>Where a binding acts.</summary>
    public enum InputContext {
        /// <summary>World actions (placing, harvesting, attacking): need no UI open.</summary>
        Gameplay,
        /// <summary>Keys for an open window or panel (inventory, mod window): blocked only by typing and the pause menu.</summary>
        Window,
        /// <summary>Suite-wide toggles that work under menus; still never while typing.</summary>
        Global,
    }

    public static class InputGate {
        public static InputBlock Reason(InputFacts facts){
            if(facts==null)return InputBlock.None;
            if(facts.Typing)return InputBlock.Typing;
            if(facts.MenuVisible)return InputBlock.Menu;
            if(facts.ModWindowOpen)return InputBlock.ModWindow;
            if(facts.InventoryVisible||facts.OtherGameUiVisible)return InputBlock.GameUi;
            return InputBlock.None;
        }
        public static bool Allows(InputFacts facts,InputContext context){
            switch(Reason(facts)){
                case InputBlock.None:return true;
                case InputBlock.Typing:return false;
                case InputBlock.Menu:return context==InputContext.Global;
                default:return context!=InputContext.Gameplay;
            }
        }
    }
}
