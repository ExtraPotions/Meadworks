using Burrow.Core;
using Burrow.Shared;
using Burrow.Stow;

namespace Burrow.Integration
{
    /// <summary>
    /// BS2: Burrow's "Feed source" chest tag, a ZDO bool (<see cref="IntegrationContracts.FeedSourceKey"/>) on the
    /// container's net object. Shepherd reads it through <see cref="IntegrationContracts.FeedSource"/> for
    /// "Container Selection = MarkedOnly". Burrow only stores and reports the tag; it never feeds anything.
    /// Private chests are never feed sources. Main thread only.
    /// </summary>
    internal static class FeedSources
    {
        /// <summary>The chest carries the tag and is not private. Never throws for a missing or unloaded chest.</summary>
        public static bool IsFeedSource(Container container)
        {
            if (container == null)
                return false;
            ZNetView view = container.m_nview;
            if (view == null || !view.IsValid())
                return false;
            return IntegrationRules.IsFeedSource(view.GetZDO().GetBool(IntegrationContracts.FeedSourceKey), IsPrivate(container));
        }

        /// <summary>The capability body: the argument must be a <c>Container</c>.</summary>
        public static bool Query(object container) => container is Container chest && IsFeedSource(chest);

        /// <summary>Toggles the tag on the open chest. The local client owns an open chest; a viewed chest is refused.</summary>
        public static void ToggleOpen()
        {
            Container open = StowTargets.Open;
            if (open == null)
            {
                StowTargets.SayNoOpen();
                return;
            }
            ZNetView view = open.m_nview;
            bool owner = view != null && view.IsValid() && view.IsOwner();
            if (!IntegrationRules.CanTagFeedSource(IsPrivate(open), owner))
            {
                Messages.Center(IsPrivate(open) ? StowWords.FeedSourcePrivate : SharedWords.ReadOnly);
                return;
            }
            ZDO zdo = view.GetZDO();
            bool on = !zdo.GetBool(IntegrationContracts.FeedSourceKey);
            zdo.Set(IntegrationContracts.FeedSourceKey, on);
            Messages.Center(StowWords.Format(on ? StowWords.FeedSourceOn : StowWords.FeedSourceOff, StowTargets.Name(open)));
        }

        private static bool IsPrivate(Container container) => container.m_privacy == Container.PrivacySetting.Private;
    }
}
