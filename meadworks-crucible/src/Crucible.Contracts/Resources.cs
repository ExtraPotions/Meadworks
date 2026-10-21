using System;
using System.Collections.Generic;
namespace ExtraPotions.Meadworks.Contracts {
 public sealed class ResourceContext {
  public string ActorId{get;}public string WorldId{get;}public string RequestId{get;}
  public ResourceContext(string actorId,string worldId,string requestId){if(string.IsNullOrWhiteSpace(actorId)||string.IsNullOrWhiteSpace(worldId)||string.IsNullOrWhiteSpace(requestId))throw new ArgumentException("Resource context requires actor, world and request identity");ActorId=actorId;WorldId=worldId;RequestId=requestId;}
 }
 public sealed class ResourceCost {
  public string ItemId{get;}public int Amount{get;}
  public ResourceCost(string itemId,int amount){if(string.IsNullOrWhiteSpace(itemId)||amount<=0)throw new ArgumentException("A resource cost requires an item and positive amount");ItemId=itemId;Amount=amount;}
 }
 public enum ReservationState {Reserved,Committed,RolledBack}
 // Providers must reserve atomically across all costs, obey access permissions, and
 // commit atomically. On a failed commit, rollback must release all held resources.
 // Successful commit is final; repeated commit/rollback/dispose must not charge again.
 public interface IResourceReservation:IDisposable {ReservationState State{get;}void Commit();void Rollback();}
 public interface IResourceProvider {
  int GetAvailable(ResourceContext context,string itemId);
  bool TryReserve(ResourceContext context,IReadOnlyList<ResourceCost> costs,out IResourceReservation? reservation);
 }
}
