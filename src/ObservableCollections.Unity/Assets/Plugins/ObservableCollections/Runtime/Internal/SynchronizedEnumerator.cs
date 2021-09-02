using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace ObservableCollections.Internal
{
    internal class SynchronizedEnumerator<T> : IEnumerator<T>
    {
        bool isDisposed;
        readonly object gate;
        readonly bool lockTaken;
        readonly IEnumerator<T> enumerator;

        public SynchronizedEnumerator(object gate, IEnumerator<T> enumerator)
        {
            this.gate = gate;
            this.enumerator = enumerator;
            Monitor.Enter(gate, ref lockTaken);
        }

        public T Current => enumerator.Current;

        object IEnumerator.Current => Current;
        public bool MoveNext() => enumerator.MoveNext();
        public void Reset() => enumerator.Reset();

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
