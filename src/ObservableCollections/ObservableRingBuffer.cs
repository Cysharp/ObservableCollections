namespace ObservableCollections
{

    public sealed partial class ObservableRingBuffer<T>
    {
        readonly RingBuffer<T> buffer;

        // TODO:SyncRoot

        public ObservableRingBuffer()
        {
            this.buffer = new RingBuffer<T>();
        }

        public ObservableRingBuffer(IEnumerable<T> collection)
        {
            this.buffer = new RingBuffer<T>(collection);
        }

        public int Count => buffer.Count;

        public T this[int index]
        {
            get
            {
                return this.buffer[index];
            }
            set
            {
                this.buffer[index] = value;
            }
        }

        public void AddLast(T item)
        {
        }

        public void AddLastRange(T[] items)
        {

        }
    }
}
