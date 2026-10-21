using System;
namespace ExtraPotions.Meadworks.Crucible {
    public sealed class UiDescriptor {
        public string Title { get; }
        public string Brand { get; }
        public string KeybindHelp { get; }
        public UiDescriptor(string title,string brand,string keybindHelp) {
            if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(brand)||keybindHelp==null)throw new ArgumentException("Invalid UI descriptor.");
            Title=title;Brand=brand;KeybindHelp=keybindHelp;
        }
    }
}
