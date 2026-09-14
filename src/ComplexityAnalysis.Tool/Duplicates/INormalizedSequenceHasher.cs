using System.Collections.Generic;

namespace ComplexityAnalysis.Tool.Duplicates;

internal interface INormalizedSequenceHasher
{
    ulong Hash(IReadOnlyList<NormalizedToken> tokens, int start, int length);
}
