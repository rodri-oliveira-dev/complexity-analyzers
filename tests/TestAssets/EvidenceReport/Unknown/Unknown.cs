using System;

public sealed class Unknown
{
    public int WaitForClock()
    {
        var index = 0;
        while (DateTime.UtcNow.Ticks > index)
        {
            index++;
        }

        return index;
    }
}
