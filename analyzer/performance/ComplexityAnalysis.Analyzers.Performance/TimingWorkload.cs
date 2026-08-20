using System.Collections.Generic;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Performance;

public sealed class TimingWorkload
{
    public int Constant00() => 0;
    public int Constant01() => 1;
    public int Constant02() => 2;
    public int Constant03() => 3;
    public int Constant04() => 4;
    public int Constant05() => 5;
    public int Constant06() => 6;
    public int Constant07() => 7;
    public int Constant08() => 8;
    public int Constant09() => 9;
    public int Constant10() => 10;
    public int Constant11() => 11;
    public int Constant12() => 12;
    public int Constant13() => 13;
    public int Constant14() => 14;
    public int Constant15() => 15;

    public int LoopHeavy(int[] values)
    {
        var total = 0;
        foreach (var outer in values)
        {
            for (var inner = 0; inner < values.Length; inner++)
            {
                total += outer + values[inner];
            }
        }

        return total;
    }

    public int LinqHeavy(IEnumerable<int> values)
    {
        return values
            .Where(value => value >= 0)
            .OrderBy(value => value)
            .Select(value => value + 1)
            .ToList()
            .Count;
    }

    public void SharedRoot00(int[] values) => SharedCallee(values);
    public void SharedRoot01(int[] values) => SharedCallee(values);
    public void SharedRoot02(int[] values) => SharedCallee(values);
    public void SharedRoot03(int[] values) => SharedCallee(values);

    public void DeepRoot(int[] values) => Deep01(values);

    public int SupportedRecursive(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        return SupportedRecursive(n - 1) + SupportedRecursive(n - 2);
    }

    public int UnsupportedRecursive(int n)
    {
        return UnsupportedRecursive(n - 1);
    }

    private void SharedCallee(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }

    private void Deep01(int[] values) => Deep02(values);
    private void Deep02(int[] values) => Deep03(values);
    private void Deep03(int[] values) => Deep04(values);
    private void Deep04(int[] values) => Deep05(values);
    private void Deep05(int[] values) => Deep06(values);

    private void Deep06(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }
}
