using System;
namespace Wayfarer.Core {
    /// <summary>A value computed at most once per frame: the client asks for the Mischief Travel decision several times a frame.</summary>
    public sealed class PerFrame<T> {
        readonly Func<int> frame;readonly Func<T> compute;int cachedFrame;bool cached;T value=default!;
        public PerFrame(Func<int> frame,Func<T> compute){this.frame=frame??throw new ArgumentNullException(nameof(frame));this.compute=compute??throw new ArgumentNullException(nameof(compute));}
        public T Value{get{var now=frame();if(!cached||now!=cachedFrame){value=compute();cachedFrame=now;cached=true;}return value;}}
    }
}
