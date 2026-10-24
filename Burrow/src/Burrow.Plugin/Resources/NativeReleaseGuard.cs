// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using Burrow.Core;

namespace Burrow.Resources {
    internal static class NativeReleaseGuard {
        internal static bool MayHaveHold(string source){
            try{return ContainerScan.AnyTracked(source,Matches);}
            // A changed collection or failed native read must retain the original resolver.
            catch{return true;}
        }
        static bool Matches(Container chest,string source){
            var view=chest.m_nview;
            if(view==null||!view.IsValid())return false;
            var zdo=view.GetZDO();if(zdo==null)return true;
            string hold=zdo.GetString(NativeBundleStore.HoldKey);
            return hold==source||!ReferenceEquals(chest.m_nview,view)||!view.IsValid()||!ReferenceEquals(view.GetZDO(),zdo);
        }
    }
}
