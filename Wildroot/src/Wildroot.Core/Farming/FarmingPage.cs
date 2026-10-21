// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Skills/Book/PageText.cs and SkillPage.cs.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Wildroot namespace; only the number format and the perk state, for MeadworksSkills page lines.
using System;
using System.Globalization;
namespace ExtraPotions.Wildroot {
 /// <summary>
 /// The Farming page's number and perk formats, as the Solo profile's GrindstoneSkills book writes them: a share as a
 /// percent with at most one decimal, and a perk's name followed by "unlocked" in green or "at level N" in a muted brown.
 /// </summary>
 public static class FarmingPage {
  public const string UnlockedColour="#9BD46A",LockedColour="#A08E74";
  public static string Percent(float share)=>(share*100f).ToString("0.#",CultureInfo.InvariantCulture)+"%";
  /// <summary>The perk's line, or null when its level is above 100 (the perk is turned off and not listed).</summary>
  public static string? PerkLine(string name,int perkLevel,float level,string unlocked,string atLevel){
   if(perkLevel>FarmingPerks.MaxLevel)return null;
   if(perkLevel<=0)return name;
   return level>=perkLevel?name+"  <color="+UnlockedColour+">"+unlocked+"</color>":name+"  <color="+LockedColour+">"+string.Format(CultureInfo.InvariantCulture,atLevel,perkLevel)+"</color>";
  }
 }
}
