// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX15 legacy host; only the exact entry drawn on the current thread has its setter redirected.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed class KitAlmanacDrawer : MonoBehaviour
    {
        static readonly Harmony patches=new Harmony(CrucibleUiPlugin.Guid+".legacyDrawer");
        static readonly Dictionary<MethodBase,int> targets=new Dictionary<MethodBase,int>();
        [ThreadStatic] static KitAlmanacDrawer drawing;
        [ThreadStatic] static bool routing;
        ConfigEntryBase entry;Action draw;Func<bool> admit;Action<object> commit;Action<Exception> failed;
        MethodInfo setter;readonly Vector3[] corners=new Vector3[4];bool disposed;
        public static KitAlmanacDrawer Create(RectTransform rect,ConfigEntryBase entry,Action draw,Func<bool> admit,Action<object> commit,Action<Exception> failed)
        {
            var host=rect.gameObject.AddComponent<KitAlmanacDrawer>();
            host.entry=entry;host.draw=draw;host.admit=admit;host.commit=commit;host.failed=failed;
            try
            {
                host.setter=AccessTools.Property(entry.GetType(),nameof(ConfigEntryBase.BoxedValue))?.GetSetMethod();
                if(host.setter==null||host.setter.IsAbstract)throw new InvalidOperationException("The entry has no interceptable native setter.");
                if(!targets.TryGetValue(host.setter,out var count))patches.Patch(host.setter,prefix:new HarmonyMethod(typeof(KitAlmanacDrawer),nameof(Assign)));
                targets[host.setter]=count+1;return host;
            }
            catch{host.disposed=true;Destroy(host);throw;}
        }
        static bool Assign(object __instance,object __0)
        {
            var host=drawing;
            if(host==null||routing||!ReferenceEquals(__instance,host.entry))return true;
            if(host.disposed||host.admit?.Invoke()!=true)return false;
            routing=true;
            try{host.commit(__0);}finally{routing=false;}
            return false;
        }
        Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);var canvas=rect.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return new Rect(low.x,Screen.height-high.y,high.x-low.x,high.y-low.y);
        }
        void OnGUI()
        {
            if(disposed||!gameObject.activeInHierarchy||admit?.Invoke()!=true)return;
            Rect full=ScreenRect((RectTransform)transform),clip=full;
            var scroll=GetComponentInParent<ScrollRect>();
            if(scroll?.viewport!=null)
            {
                var viewport=ScreenRect(scroll.viewport);
                clip=Rect.MinMaxRect(Mathf.Max(full.xMin,viewport.xMin),Mathf.Max(full.yMin,viewport.yMin),Mathf.Min(full.xMax,viewport.xMax),Mathf.Min(full.yMax,viewport.yMax));
            }
            if(clip.width<=0||clip.height<=0)return;
            var previous=drawing;var colour=GUI.color;var background=GUI.backgroundColor;var content=GUI.contentColor;
            bool grouped=false,area=false;
            try
            {
                drawing=this;GUI.contentColor=Color.white;GUI.backgroundColor=new Color(.24f,.18f,.12f,1);
                GUI.BeginGroup(clip);grouped=true;
                GUILayout.BeginArea(new Rect(full.x-clip.x,full.y-clip.y,full.width,full.height));area=true;draw();
            }
            catch(Exception error){failed?.Invoke(error.GetBaseException());}
            finally
            {
                if(area)GUILayout.EndArea();if(grouped)GUI.EndGroup();
                GUI.color=colour;GUI.backgroundColor=background;GUI.contentColor=content;drawing=previous;
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;enabled=false;
            if(setter!=null&&targets.TryGetValue(setter,out int count))
            {
                if(count==1){targets.Remove(setter);patches.Unpatch(setter,HarmonyPatchType.Prefix,patches.Id);}
                else targets[setter]=count-1;
            }
            draw=null;admit=null;commit=null;failed=null;entry=null;Destroy(this);
        }
        void OnDestroy()=>Dispose();
    }
}
