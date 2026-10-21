using ExtraPotions.Meadworks.Crucible;
namespace Wayfarer.Core {
    public static class MeadworksIntegration {
        public static ProductMetadata Metadata()=>new ProductMetadata("com.meadworks.wayfarer","Wayfarer",new System.Version(WayfarerIdentity.Version),new System.Version(0,1),new System.Version(0,1),MultiplayerPolicy.RequiredOnAllPeers,WayfarerIdentity.Family);
        public static UiDescriptor Ui()=>new UiDescriptor("Wayfarer","Meadworks","M");
    }
}
