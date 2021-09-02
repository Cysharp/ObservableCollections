using System;
using System.Collections.Generic;
using System.Text;

namespace System.Runtime.CompilerServices
{
    internal static class RuntimeHelpersEx
    {
        internal static bool IsReferenceOrContainsReferences<T>()
        {
#if NETSTANDARD2_0
            return true;
#else
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#endif
        }
    }
}
