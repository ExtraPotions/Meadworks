using System;
using System.Collections.Concurrent;
using System.Threading;
namespace ExtraPotions.Meadworks.Crucible {
    internal sealed class RequestDispatcher:IDisposable {
        readonly ConcurrentQueue<Action> queue=new ConcurrentQueue<Action>();int count;volatile bool stopped;
        internal bool Enqueue(Action action){if(stopped)return false;if(Interlocked.Increment(ref count)>256){Interlocked.Decrement(ref count);return false;}queue.Enqueue(action);return true;}
        internal void Update(){for(int i=0;i<256&&queue.TryDequeue(out var action);i++){Interlocked.Decrement(ref count);if(!stopped)try{action();}catch{ /* Reject malformed/disconnected work without logging payloads. */ }}}
        public void Dispose(){stopped=true;while(queue.TryDequeue(out _))Interlocked.Decrement(ref count);}
    }
}
