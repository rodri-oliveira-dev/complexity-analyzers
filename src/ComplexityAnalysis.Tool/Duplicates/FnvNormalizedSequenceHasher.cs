using System;
using System.Collections.Generic;
using System.Text;

namespace ComplexityAnalysis.Tool.Duplicates;

internal sealed class FnvNormalizedSequenceHasher : INormalizedSequenceHasher
{
    public ulong Hash(IReadOnlyList<NormalizedToken> tokens, int start, int length)
    {
        ulong hash = 14695981039346656037UL;
        for (int index = start; index < start + length; index++)
        {
            foreach (byte value in Encoding.UTF8.GetBytes(tokens[index].Value))
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }

            hash ^= 0xff;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
