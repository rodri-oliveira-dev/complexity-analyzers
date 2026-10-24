using System.Collections.Generic;

public sealed class Known
{
    public int CountMatches(int[] values, List<int> other)
    {
        var count = 0;
        foreach (var value in values)
        {
            if (other.Contains(value))
            {
                count++;
            }
        }

        return count;
    }
}
