namespace ObservableCollections
{

    public sealed partial class ObservableRingBuffer<T>
    {
        // TODO:not yet.
        readonly T[] buffer;

        int head;
        int count;

        public ObservableRingBuffer(int capacity)
        {
            this.buffer = new T[capacity];
        }

        public int Count => count;

        public T this[int index]
        {
            get
            {
                var i = (head + index) % buffer.Length;
                return this.buffer[i];
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
