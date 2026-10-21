// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: owned lifecycle, portable emission policy and live ember fallback.
using System;
namespace ExtraPotions.Wildblood.Visuals;
public static class FlameVisualPolicy
{
    public const float DefaultLife=7f, DefaultRadius=1f;
    public static float BodyScale(float radius)=>Math.Max(.5f,radius*2f)*.4f;
    public static bool BodyVisible(bool cloaked,bool cloning)=>!cloaked&&!cloning;
    public static bool DrawDecals(bool flamesAvailable,float density)=>!flamesAvailable||Density(density)<=0f;
    static float Density(float value)=>Math.Max(0f,Math.Min(1f,value));
    public static float PerPatchRate(float densityAdjustedRate,float density)=>densityAdjustedRate/Math.Max(Density(density),.01f)*.5f;
    public static int Take(ref float owed,float rate,int patches,float elapsed,float density)
    {
        float share=patches*elapsed*Density(density);
        owed=share>0f?owed+rate*share:0f;
        int count=(int)Math.Floor(owed);owed-=count;return count;
    }
}
