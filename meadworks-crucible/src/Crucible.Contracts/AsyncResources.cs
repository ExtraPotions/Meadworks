using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace ExtraPotions.Meadworks.Contracts {
    public sealed class AsyncResourceContext {
        public string ActorId{get;}public long WorldId{get;}public string SessionId{get;}public string RequestId{get;}
        public AsyncResourceContext(string actorId,long worldId,string sessionId,string requestId){Text(actorId);Text(sessionId);Text(requestId);if(worldId==0)throw new ArgumentException("Missing world.");ActorId=actorId;WorldId=worldId;SessionId=sessionId;RequestId=requestId;}
        static void Text(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.IndexOfAny(new[]{'\0','\r','\n'})>=0)throw new ArgumentException("Invalid resource correlation identity.");}
    }
    public enum AsyncReservationState { Pending,Reserved,CommitPending,Committed,RolledBack,Failed,RecoveryRequired }
    public sealed class ResourceReservationResult {
        public AsyncReservationState State{get;}public IAsyncResourceReservation? Reservation{get;}
        public ResourceReservationResult(AsyncReservationState state,IAsyncResourceReservation? reservation){
            if(!Enum.IsDefined(typeof(AsyncReservationState),state))throw new ArgumentException("Unknown resource state.");
            if(reservation==null&&(state==AsyncReservationState.Reserved||state==AsyncReservationState.CommitPending||state==AsyncReservationState.Committed||state==AsyncReservationState.RecoveryRequired))throw new ArgumentException("A hold or decision requires its reservation identity.");
            State=state;Reservation=reservation;
        }
    }
    // v2: context values are claims, validated against the authenticated request
    // context by the provider. A lost transport after a decision is not a refund.
    public interface IAsyncResourceProvider {
        Task<int> GetAvailableAsync(AsyncResourceContext context,string itemId,CancellationToken cancellation);
        Task<ResourceReservationResult> TryReserveAsync(AsyncResourceContext context,IReadOnlyList<ResourceCost> costs,CancellationToken cancellation);
    }
    public interface IAsyncResourceReservation:IDisposable {
        AsyncResourceContext Identity{get;}AsyncReservationState State{get;}
        Task<AsyncReservationState> CommitAsync(CancellationToken cancellation);
        Task<AsyncReservationState> RollbackAsync(CancellationToken cancellation);
        Task<AsyncReservationState> GetStatusAsync(CancellationToken cancellation);
        // Dispose releases only undecided holds. CommitPending/RecoveryRequired
        // remain in the provider's durable ledger and must be reconciled there.
    }
}
