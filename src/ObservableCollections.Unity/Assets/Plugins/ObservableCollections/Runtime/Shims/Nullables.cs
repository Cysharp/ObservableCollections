using System;
using System.Collections.Generic;
using System.Text;

#if (NETSTANDARD2_0 || NET_STANDARD_2_0 || NET_4_6) && !UNITY_2021_1_OR_NEWER

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