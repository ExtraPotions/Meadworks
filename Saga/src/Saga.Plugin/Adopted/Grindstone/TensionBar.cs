// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// The adopted fight owns tension and grace; presentation uses Crucible.UI IX01.
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine;
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone {
    public static class TensionBar {
        static KitMeter? meter;static float shownUntil;
        public static void Show(FloatFight fight) {
            if(!Plugin.AllowsUi||Hud.instance?.m_rootObject==null||fight==null)return;
            if(meter?.Root==null) {
                meter?.Dispose();meter=KitMeter.Create((RectTransform)Hud.instance.m_rootObject.transform,"saga.fishing_tension",new Vector2(0,-90),new Vector2(240,24));
                if(meter==null)return;meter.Root.anchorMin=meter.Root.anchorMax=new Vector2(0.5f,0.5f);meter.Root.pivot=new Vector2(0.5f,1f);
            }
            shownUntil=Time.unscaledTime+0.25f;meter.Root.gameObject.SetActive(true);
            var text=fight.InGrace?SagaText.Text("tension_grace")+" "+Mathf.CeilToInt(fight.GraceUntil-Time.time):fight.Spent?SagaText.Text("tension_spent"):fight.Thrashing?SagaText.Text("tension_thrash"):SagaText.Text("tension_line");
            meter.Set(fight.Tension,1,text,Color.Lerp(new Color(0.45f,0.8f,0.35f),new Color(0.9f,0.2f,0.15f),fight.Tension));
        }
        public static void Tick(){if(Time.unscaledTime>shownUntil)Hide();}
        public static void Hide(){if(meter?.Root!=null)meter.Root.gameObject.SetActive(false);}
        public static void Dispose(){meter?.Dispose();meter=null;}
    }
}
