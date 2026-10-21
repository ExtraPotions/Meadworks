using System;

namespace ExtraPotions.Mantle.Embedded.EliteCraftingLink
{
    /// <summary>
    /// One endpoint of the API as a typed delegate, bound on first use once EliteCrafting is found
    /// (<see cref="ApiBinding.Create{T}"/>): null while EliteCrafting is absent, and for good when it is older or lacks
    /// the endpoint.
    /// </summary>
    internal sealed class Endpoint<T> where T : Delegate
    {
        private readonly string _name;
        private T? _call;
        private bool _bound;

        public Endpoint(string name)
        {
            _name = name;
        }

        public T? Call
        {
            get
            {
                if (!_bound && ApiBinding.Api != null)
                {
                    _bound = true;
                    _call = ApiBinding.Create<T>(_name);
                }
                return _call;
            }
        }
    }

    /// <summary>
    /// Calls a bound endpoint, or answers with the fallback (false, null, 0) when it is not bound or throws. Overloads
    /// per argument count, so the wrappers allocate nothing.
    /// </summary>
    internal static class Safe
    {
        public static R Call<R>(Func<R>? call, R fallback)
        {
            try
            {
                return call != null ? call() : fallback;
            }
            catch (Exception e)
            {
                ApiBinding.Failed(e);
                return fallback;
            }
        }

        public static R Call<A, R>(Func<A, R>? call, A a, R fallback)
        {
            try
            {
                return call != null ? call(a) : fallback;
            }
            catch (Exception e)
            {
                ApiBinding.Failed(e);
                return fallback;
            }
        }

        public static R Call<A, B, R>(Func<A, B, R>? call, A a, B b, R fallback)
        {
            try
            {
                return call != null ? call(a, b) : fallback;
            }
            catch (Exception e)
            {
                ApiBinding.Failed(e);
                return fallback;
            }
        }

        public static R Call<A, B, C, R>(Func<A, B, C, R>? call, A a, B b, C c, R fallback)
        {
            try
            {
                return call != null ? call(a, b, c) : fallback;
            }
            catch (Exception e)
            {
                ApiBinding.Failed(e);
                return fallback;
            }
        }
    }
}
