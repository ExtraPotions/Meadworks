using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// What a kit text field (KX01) is for. Search: Escape clears it, Enter keeps it, both let go. Numeric: digits only,
    /// a click selects the whole amount, Enter or a click elsewhere applies it, Escape restores the old one. Text: a plain
    /// single-line value (Almanac AX04) committed on Enter or when the field lets go, Escape restores it.
    /// </summary>
    public enum KitFieldMode { Search, Numeric, Text, Password }

    /// <summary>The kit text field's look and limits (kindle-design.md 3.2): the stat sheet's dark rounded inset.</summary>
    public static class KitInputLayout {
        public const float TextSize=16f;
        /// <summary>Text area inset from the field's left and right edges...</summary>
        public const float InsetX=8f;
        /// <summary>...and from its top and bottom.</summary>
        public const float InsetY=2f;
        public const int SearchLength=40;
        public const int NumericDigits=4;
        /// <summary>A plain text field's limit (Almanac string settings).</summary>
        public const int TextLength=256;
        /// <summary>Seconds after the last keystroke before the change event fires.</summary>
        public const float Debounce=0.12f;
        public const int CaretWidth=2;

        public static readonly KitColor Fill=KitPalette.SheetInset;
        public static readonly KitColor Text=KitPalette.SheetLabel;
        public static readonly KitColor Placeholder=KitPalette.TabDimText;
        public static readonly KitColor Caret=KitPalette.SheetValue;
        public static readonly KitColor Selection=new KitColor(KitPalette.SheetValue.R,KitPalette.SheetValue.G,KitPalette.SheetValue.B,0.35f);
        /// <summary>Text of a value the product's validator rejects (AX04), the tracker's warning red.</summary>
        public static readonly KitColor Invalid=KitColor.FromBytes(0xFF,0x6A,0x5A);
        /// <summary>The inset behind a rejected value: the sheet inset warmed towards red.</summary>
        public static readonly KitColor InvalidFill=new KitColor(0.32f,0.05f,0.03f,0.5f);

        public static KitColor TextColour(bool valid)=>valid?Text:Invalid;
        public static KitColor FillColour(bool valid)=>valid?Fill:InvalidFill;
    }

    /// <summary>How an edit ended: the text the field shows now and what the product should do with it.</summary>
    public readonly struct KitFieldEdit {
        public string Text { get; }
        /// <summary>The product takes <see cref="Text"/> as the new value.</summary>
        public bool Commit { get; }
        /// <summary>Escape emptied a search.</summary>
        public bool Cleared { get; }
        /// <summary>Escape put the value from before the edit back.</summary>
        public bool Restored { get; }
        /// <summary>The field lets go of the focus, so hotkeys work again (every end does).</summary>
        public bool Release { get; }
        public KitFieldEdit(string text,bool commit,bool cleared,bool restored,bool release){Text=text??"";Commit=commit;Cleared=cleared;Restored=restored;Release=release;}
    }

    /// <summary>The kit text field's input rules per <see cref="KitFieldMode"/>.</summary>
    public static class KitFieldRules {
        public static int Limit(KitFieldMode mode)=>mode==KitFieldMode.Search?KitInputLayout.SearchLength:mode==KitFieldMode.Numeric?KitInputLayout.NumericDigits:KitInputLayout.TextLength;

        /// <summary>A typed character as the field takes it, or <c>'\0'</c> when refused (TextMeshPro's validator contract).</summary>
        public static char Accept(KitFieldMode mode,char added){
            if(mode==KitFieldMode.Numeric)return added>='0'&&added<='9'?added:'\0';
            return added=='\n'||added=='\r'||added=='\t'||added=='\0'?'\0':added;
        }

        public static bool SelectAllOnFocus(KitFieldMode mode)=>mode==KitFieldMode.Numeric;

        /// <summary>Text set by the product, as the field would have let it be typed.</summary>
        public static string Clip(KitFieldMode mode,string? text){
            if(string.IsNullOrEmpty(text))return "";
            var kept=new StringBuilder(text!.Length);
            int limit=Limit(mode);
            foreach(char c in text){
                if(kept.Length>=limit)break;
                if(Accept(mode,c)!='\0')kept.Append(c);
            }
            return kept.ToString();
        }

        /// <summary>The end of an edit: Escape (<paramref name="cancelled"/>) or Enter / a click elsewhere.</summary>
        public static KitFieldEdit End(KitFieldMode mode,string? text,string? before,bool cancelled){
            text??="";before??="";
            if(!cancelled)return new KitFieldEdit(text,true,false,false,true);
            if(mode==KitFieldMode.Password)return new KitFieldEdit("",false,true,false,true);
            if(mode==KitFieldMode.Search)return new KitFieldEdit("",text.Length>0,text.Length>0,false,true);
            return new KitFieldEdit(before,false,false,true,true);
        }

        /// <summary>
        /// A typed amount within <paramref name="min"/> and <paramref name="max"/>: an empty or non-numeric entry keeps
        /// <paramref name="fallback"/> (itself kept in range), a number too long for an int is the maximum.
        /// </summary>
        public static int Amount(string? text,int fallback,int min,int max){
            if(max<min)throw new ArgumentOutOfRangeException(nameof(max));
            int Clamp(int v)=>v<min?min:v>max?max:v;
            var trimmed=(text??"").Trim();
            if(trimmed.Length==0)return Clamp(fallback);
            foreach(char c in trimmed)if(c<'0'||c>'9')return Clamp(fallback);
            return long.TryParse(trimmed,out var value)&&value<=int.MaxValue?Clamp((int)value):max;
        }
    }

    /// <summary>The change event waits until typing pauses: every keystroke restarts the delay, and it fires once.</summary>
    public sealed class KitDebounce {
        float due=-1f;
        public float Delay { get; }
        public KitDebounce(float delay=KitInputLayout.Debounce){if(delay<0f)throw new ArgumentOutOfRangeException(nameof(delay));Delay=delay;}
        public bool Pending=>due>=0f;
        public void Touch(float now){due=now+Delay;}
        public void Cancel(){due=-1f;}
        /// <summary>True once, at the first call at or after the delay.</summary>
        public bool Due(float now){
            if(due<0f||now<due)return false;
            due=-1f;
            return true;
        }
    }

    /// <summary>What the typing guard does this frame (see <see cref="KitTypingRules"/>).</summary>
    public readonly struct KitTypingVerdict {
        public bool Typing { get; }
        /// <summary>Reset the game's Use and Inventory presses so typing E or Tab does not close the inventory.</summary>
        public bool DropGameKeys { get; }
        /// <summary>Skip the inventory's update this frame (Escape, or a pad leaving the field), so only the field reacts.</summary>
        public bool SkipInventoryUpdate { get; }
        /// <summary>Let go of the focused field (its text is kept).</summary>
        public bool LeaveField { get; }
        /// <summary>Consume pad B and Y so the game does not also close what is under the field.</summary>
        public bool ConsumePad { get; }
        public KitTypingVerdict(bool typing,bool drop,bool skip,bool leave,bool consume){Typing=typing;DropGameKeys=drop;SkipInventoryUpdate=skip;LeaveField=leave;ConsumePad=consume;}
    }

    /// <summary>
    /// The kit field's typing guard (KX01, OpenKeep TypingGuard behavior rebuilt on C07): the game closes the inventory on
    /// its Use key, its Inventory key and Escape with no regard for a focused field. While a kit field is focused, or was
    /// at the end of the last frame (it may have handled Escape and let go already), those presses are dropped, Escape
    /// only leaves the field, and pad B or Y leaves the field instead of closing the inventory (a pad without Steam's
    /// keyboard cannot type and would otherwise be stuck). Meanwhile the field counts as a C07 typing window.
    /// </summary>
    public static class KitTypingRules {
        public static IReadOnlyList<string> GameKeys { get; }=new ReadOnlyCollection<string>(new[]{"Inventory","Use"});
        public static IReadOnlyList<PadButton> LeavePad { get; }=new ReadOnlyCollection<PadButton>(new[]{PadButton.B,PadButton.Y});

        public static KitTypingVerdict Decide(bool focusedNow,bool focusedLastFrame,bool escapeDown,bool padBDown,bool padYDown){
            if(!focusedNow&&!focusedLastFrame)return default;
            if(padBDown||padYDown)return new KitTypingVerdict(true,true,true,true,true);
            return new KitTypingVerdict(true,true,escapeDown,false,false);
        }

        /// <summary>A focused field that opted in makes the game's <c>Chat.HasFocus</c> true, for mods that only ask the chat.</summary>
        public static bool ReportsChat(bool focused,bool optedIn)=>focused&&optedIn;
    }

    public enum KitKeyboardOutcome {
        /// <summary>The cursor is in the field; the player types on a keyboard.</summary>
        Focused,
        /// <summary>Steam's on-screen keyboard opened (Big Picture, Steam Deck).</summary>
        SteamKeyboard,
        /// <summary>A pad player with no on-screen keyboard: the kit shows its fallback message (AX16).</summary>
        Unavailable,
    }

    public static class KitKeyboard {
        public static KitKeyboardOutcome Decide(bool gamepad,bool keyboardOpened)=>
            keyboardOpened?KitKeyboardOutcome.SteamKeyboard:gamepad?KitKeyboardOutcome.Unavailable:KitKeyboardOutcome.Focused;
    }
}
