using System;
using System.Collections.Generic;
using System.Text;

#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis
{
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue)
        {
        }
    }

    internal sealed class DoesNotReturnAttribute : Attribute
    {
        public DoesNotReturnAttribute()
        {
        }
    }
}

#endif