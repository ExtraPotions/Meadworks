using System.Collections.Generic;
using Mantle.Core;
using Mantle.Elite;

namespace Mantle.Panels
{
    /// <summary>
    /// Everything the Gear tab's stat sheet lists (<see cref="GearStats"/>): the core numbers, a section per weapon or
    /// shield in hand (<see cref="HeldStatLines"/>), the game's resistances, the gear's other modifiers (<see cref="GearStatLines"/>), then Epic Loot's active effects when installed, a section per
    /// group (<see cref="EffectGroups"/>), each effect once with Epic Loot's total in Epic Loot's own words
    /// (<see cref="EpicLootLink"/>). Epic Loot's effects that also feed the game's numbers (health, armour, carry weight,
    /// movement) are listed too: the core lines are totals, these say what the magic adds. Then EliteCrafting's
    /// inscriptions, a section per category (<see cref="EliteStatLines"/>).
    /// </summary>
    public static class SheetLines
    {
        private static readonly List<string> types = new List<string>();

        public static void Fill(Player player, StatSheet sheet)
        {
            sheet.Clear();
            if (player == null)
                return;
            GearStatLines.Core(player, sheet);
            HeldStatLines.Fill(player, sheet);
            sheet.Section(Words.StatResistances);
            GearStatLines.Resistances(player, sheet);
            sheet.Section(Words.StatGear);
            GearStatLines.Modifiers(player, sheet);
            if (EpicLootLink.ActiveTypes(player, types))
                foreach (EffectGroups.Group group in EffectGroups.Order)
                    AddGroup(player, sheet, group);
            EliteStatLines.Fill(player, sheet);
        }

        private static void AddGroup(Player player, StatSheet sheet, EffectGroups.Group group)
        {
            sheet.Section(EffectGroups.Title(group));
            foreach (string type in types)
                if (EffectGroups.Of(type) == group)
                    sheet.Add(EpicLootLink.Line(type, EpicLootLink.Total(player, type)), "");
        }
    }
}
