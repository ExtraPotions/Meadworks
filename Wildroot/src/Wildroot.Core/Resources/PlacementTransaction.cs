using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Contracts;
namespace ExtraPotions.Wildroot {
 public enum PlacementResultKind {Committed,InsufficientResources,Cancelled,CreateFailed,CommitFailed,RecoveryRequired}
 public sealed class PlacementResult {public PlacementResultKind Kind{get;}public string RequestId{get;}public PlacementResult(PlacementResultKind kind,string requestId){Kind=kind;RequestId=requestId;} }
 public sealed class PlacementOperation {
  public ResourceContext Context{get;}public IReadOnlyList<ResourceCost> Costs{get;}public Func<bool> IsActive{get;}
  public PlacementOperation(ResourceContext context,IReadOnlyList<ResourceCost> costs,Func<bool> isActive){Context=context??throw new ArgumentNullException(nameof(context));if(costs==null)throw new ArgumentNullException(nameof(costs));var copy=new List<ResourceCost>();foreach(var cost in costs)copy.Add(cost??throw new ArgumentException("Null cost"));Costs=copy.AsReadOnly();IsActive=isActive??throw new ArgumentNullException(nameof(isActive));}
 }
 public interface IPlacementHandle {void Compensate();}
 public interface IPlacementBackend {IPlacementHandle Create(PlacementOperation operation);}
 public sealed class PlacementTransaction {
  readonly object gate=new object();
  readonly Dictionary<(string,string,string),PlacementResult> completed=new Dictionary<(string,string,string),PlacementResult>();
  readonly HashSet<(string,string,string)> pending=new HashSet<(string,string,string)>();
  // One coordinator per bounded batch. Completed identities remain cached for the
  // batch lifetime, including failed requests; an explicit new request may retry.
  public PlacementResult Execute(PlacementOperation operation,IPlacementBackend backend,IResourceProvider resources){
   if(operation==null)throw new ArgumentNullException(nameof(operation));if(backend==null)throw new ArgumentNullException(nameof(backend));if(resources==null)throw new ArgumentNullException(nameof(resources));
   var key=(operation.Context.ActorId,operation.Context.WorldId,operation.Context.RequestId);
   lock(gate){
    if(completed.TryGetValue(key,out var previous))return previous;
    if(!pending.Add(key))return new PlacementResult(PlacementResultKind.Cancelled,operation.Context.RequestId);
    IResourceReservation? reservation=null;IPlacementHandle? placed=null;var kind=PlacementResultKind.Cancelled;
    try{
     if(operation.IsActive()){
      if(!resources.TryReserve(operation.Context,operation.Costs,out reservation))kind=PlacementResultKind.InsufficientResources;
      else if(reservation==null||reservation.State!=ReservationState.Reserved)kind=PlacementResultKind.RecoveryRequired;
      else if(operation.IsActive()){
       try{placed=backend.Create(operation);if(placed==null)throw new InvalidOperationException("Placement backend returned no handle");}
       catch{kind=PlacementResultKind.CreateFailed;}
       if(placed!=null){
        if(operation.IsActive()){
         try{reservation.Commit();kind=reservation.State==ReservationState.Committed?PlacementResultKind.Committed:PlacementResultKind.CommitFailed;if(kind==PlacementResultKind.Committed)(placed as IConfirmedPlacementHandle)?.Confirm();}
         catch{kind=reservation.State==ReservationState.Committed?PlacementResultKind.Committed:PlacementResultKind.CommitFailed;}
        }else kind=PlacementResultKind.Cancelled;
       }
      }
     }
    }catch{kind=PlacementResultKind.RecoveryRequired;}
    finally{
     // Once the reservation is Committed the placement is paid for: it is never compensated
     // and the result is Committed, even when a later status read or Confirm misbehaved.
     if(kind!=PlacementResultKind.Committed&&reservation?.State==ReservationState.Committed)kind=PlacementResultKind.Committed;
     if(placed!=null&&kind!=PlacementResultKind.Committed){try{placed.Compensate();}catch{kind=PlacementResultKind.RecoveryRequired;}}
     if(reservation!=null){try{if(reservation.State==ReservationState.Reserved)reservation.Rollback();reservation.Dispose();}catch{kind=PlacementResultKind.RecoveryRequired;}}
     pending.Remove(key);
    }
    var result=new PlacementResult(kind,operation.Context.RequestId);completed.Add(key,result);return result;
   }
  }
 }
}
