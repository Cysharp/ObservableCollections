using System.Collections.Generic;

namespace ObservableCollections
{
    public sealed partial class ObservableLinkedList<T>
    {
        // TODO:not yet
        readonly LinkedList<T> list;

        public ObservableLinkedList(LinkedList<T> list)
        {
            this.list = list;
        }
    }
}
