namespace ObservableCollections
{
    public sealed partial class ObservableRingBuffer<T>
    {
        // TODO:not yet.
        readonly T[] buffer;

        public ObservableRingBuffer(int capacity)
        {
            this.buffer = new T[capacity];
        }

        public int Count => buffer.Length;

        public T this[int index]
        {
            get
            {
                return this.buffer[index];
            }
            set
            {
            }
        }

        public void AddLast()
        {
            // AddLast
            // AddFirst
            //new LinkedList<int>().remo
            //new Stack<int>().Push
        }

        public void AddFirst()
        {
        }

        public void RemoveLast()
        {
        }

        public void RemoveFirst()
        {
        }

        // GetReverseEnumerable
    }
}
