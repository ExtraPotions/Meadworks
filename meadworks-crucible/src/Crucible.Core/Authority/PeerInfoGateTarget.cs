using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The Jotunn method the peer-info gate observes to learn that a connection sent its mod list. The gate's prefix
    /// binds the connection positionally (<c>__0</c>), so only the method's shape matters, never its parameter names
    /// (Jotunn 2.30.2 names it <c>sender</c>).
    /// </summary>
    public static class PeerInfoGateTarget {
        public const string JotunnType="Jotunn.Utils.ModCompatibility",JotunnMethod="RPC_Jotunn_ReceiveVersionData";

        /// <summary>True when the method is static and takes exactly (ZRpc, ZPackage), by simple type name.</summary>
        public static bool Fits(bool isStatic,IReadOnlyList<string> parameterTypes)=>
            isStatic&&parameterTypes!=null&&parameterTypes.Count==2&&parameterTypes[0]=="ZRpc"&&parameterTypes[1]=="ZPackage";
    }
}
