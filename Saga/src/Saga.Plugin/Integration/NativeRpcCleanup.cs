// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
namespace ExtraPotions.Saga.Runtime {
    internal static class NativeRpcCleanup {
        sealed class ViewWrite {public WeakReference<ZNetView> View=null!;public string Name="";public object Handler=null!;}
        static readonly List<ViewWrite> views=new List<ViewWrite>();
        static readonly List<Tuple<IDictionary,int,object>> routed=new List<Tuple<IDictionary,int,object>>();
        public static void Track(ZNetView view,string name){var key=name.GetStableHashCode();if(views.Count%128==127)views.RemoveAll(write=>!write.View.TryGetTarget(out var prior)||prior==null);if(view!=null&&view.m_functions.TryGetValue(key,out var handler))views.Add(new ViewWrite{View=new WeakReference<ZNetView>(view),Name=name,Handler=handler});}
        // Source routed registrations run in ZNet.Awake, before a scene is required.
        [HarmonyPatch(typeof(ZNet),nameof(ZNet.Awake))]static class Net {
            [HarmonyPriority(Priority.Last)]static void Postfix(){var rpc=ZRoutedRpc.instance;if(rpc==null)return;var dictionary=(IDictionary)rpc.m_functions;foreach(var name in new[]{Keys.RpcWoodCredit,Keys.RpcWoodCallout,Keys.RpcFishCallout,Keys.RpcFishAnnounce,Keys.RpcMineCallout}){var key=name.GetStableHashCode();if(dictionary.Contains(key))routed.Add(Tuple.Create(dictionary,key,dictionary[key]!));}}
        }
        public static void Shutdown(){foreach(var write in views)if(write.View.TryGetTarget(out var view)&&view!=null&&view.m_functions.TryGetValue(write.Name.GetStableHashCode(),out var handler)&&ReferenceEquals(handler,write.Handler))view.Unregister(write.Name);views.Clear();foreach(var write in routed)if(write.Item1.Contains(write.Item2)&&ReferenceEquals(write.Item1[write.Item2],write.Item3))write.Item1.Remove(write.Item2);routed.Clear();}
    }
}
