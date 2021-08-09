using System.Collections.Generic;

namespace ObservableCollections
{
    public sealed partial class ObservableStack<T>
    {
        // TODO:not yet.
        readonly Stack<T> stack;

        public ObservableStack(Stack<T> stack)
        {
            this.stack = stack;
        }

        public void Push(T item)
        {
            stack.Push(item);
        }

        public void Pop(T item)
        {
            stack.Pop();
        }
    }
}
