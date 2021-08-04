using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ObservableCollections.Internal
{
    internal class SynchronizedViewEnumerator<T, TView> : IEnumerator<(T, TView)>, IDisposable
    {
        bool isDisposed;
        readonly bool lockTaken;
        readonly object gate;
        readonly IEnumerator<(T, TView)> enumerator;
        readonly ISynchronizedViewFilter<T, TView> filter;
        (T, TView) current;

        public SynchronizedViewEnumerator(object gate, IEnumerator<(T, TView)> enumerator, ISynchronizedViewFilter<T, TView> filter)
        {
            this.gate = gate;
            this.enumerator = enumerator;
            this.filter = filter;
            this.current = default;
            this.isDisposed = false;
            Monitor.Enter(gate, ref lockTaken);
        }

        public (T, TView) Current => current;
        object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                current = enumerator.Current;
                if (filter.IsMatch(current.Item1, current.Item2))
                {
                    return true;
                }
            }
            return false;
        }
        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                try
                {
                    enumerator.Dispose();
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(gate);
                    }
                }
            }
        }
    }
}