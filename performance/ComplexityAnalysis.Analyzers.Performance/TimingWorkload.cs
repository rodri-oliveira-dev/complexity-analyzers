using System.Collections.Generic;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Performance;

public sealed class TimingWorkload
{
    public int TinyStraightLine(int value) => value + 1;

    public bool TinyKnownOperation(List<int> values) => values.Contains(42);

    public int TinyLoop(int[] values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    public int SmallLoopsAndKnownOperations(List<int> values)
    {
        var total = 0;
        foreach (var value in values)
        {
            if (values.Contains(value))
            {
                total += value;
            }
        }

        return total;
    }

    public int SmallLinqPipeline(IEnumerable<int> values)
    {
        return values
            .Where(value => value >= 0)
            .Select(value => value + 1)
            .ToList()
            .Count;
    }

    public int SmallSourceCall(int[] values)
    {
        return CountPositive(values);
    }

    public int MediumNestedIteration(int[] left, int[] right)
    {
        var total = 0;
        foreach (var outer in left)
        {
            foreach (var inner in right)
            {
                total += outer + inner;
            }
        }

        return total;
    }

    public int MediumDeferredLinqConsumption(IEnumerable<int> values)
    {
        return values
            .Where(value => value >= 0)
            .OrderBy(value => value)
            .Select(value => value + 1)
            .ToList()
            .Count;
    }

    public void MediumSharedRoot00(int[] values) => SharedCallee(values);
    public void MediumSharedRoot01(int[] values) => SharedCallee(values);
    public void MediumSharedRoot02(int[] values) => SharedCallee(values);
    public void MediumSharedRoot03(int[] values) => SharedCallee(values);

    public void MediumChainRoot(int[] values) => MediumChain01(values);

    public void StressDepthRoot(int[] values) => StressDepth01(values);

    public void StressRepeatedRoot(int[] values)
    {
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
        SharedCallee(values);
    }

    public void StressFanoutRoot(int[] values)
    {
        Fanout01(values);
        Fanout02(values);
        Fanout03(values);
        Fanout04(values);
        Fanout05(values);
        Fanout06(values);
        Fanout07(values);
        Fanout08(values);
        Fanout09(values);
        Fanout10(values);
        Fanout11(values);
        Fanout12(values);
        Fanout13(values);
        Fanout14(values);
        Fanout15(values);
        Fanout16(values);
        Fanout17(values);
        Fanout18(values);
        Fanout19(values);
        Fanout20(values);
        Fanout21(values);
        Fanout22(values);
        Fanout23(values);
        Fanout24(values);
        Fanout25(values);
        Fanout26(values);
        Fanout27(values);
        Fanout28(values);
        Fanout29(values);
        Fanout30(values);
        Fanout31(values);
        Fanout32(values);
        Fanout33(values);
    }

    public int MediumSupportedRecursive(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        return MediumSupportedRecursive(n - 1) + MediumSupportedRecursive(n - 2);
    }

    public int StressUnsupportedRecursive(int n)
    {
        if (n <= 0)
        {
            return n;
        }

        return StressUnsupportedRecursive(n);
    }

    public void StressCycleRoot(int[] values) => CycleA(values);

    private int CountPositive(int[] values)
    {
        var total = 0;
        foreach (var value in values)
        {
            if (value > 0)
            {
                total++;
            }
        }

        return total;
    }

    private void SharedCallee(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }

    private void MediumChain01(int[] values) => MediumChain02(values);
    private void MediumChain02(int[] values) => MediumChain03(values);
    private void MediumChain03(int[] values) => MediumChain04(values);

    private void MediumChain04(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }

    private void StressDepth01(int[] values) => StressDepth02(values);
    private void StressDepth02(int[] values) => StressDepth03(values);
    private void StressDepth03(int[] values) => StressDepth04(values);
    private void StressDepth04(int[] values) => StressDepth05(values);
    private void StressDepth05(int[] values) => StressDepth06(values);
    private void StressDepth06(int[] values) => StressDepth07(values);
    private void StressDepth07(int[] values) => StressDepth08(values);
    private void StressDepth08(int[] values) => StressDepth09(values);
    private void StressDepth09(int[] values) => StressDepth10(values);
    private void StressDepth10(int[] values) => StressDepth11(values);
    private void StressDepth11(int[] values) => StressDepth12(values);

    private void StressDepth12(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }

    private void Fanout01(int[] values) => SharedCallee(values);
    private void Fanout02(int[] values) => SharedCallee(values);
    private void Fanout03(int[] values) => SharedCallee(values);
    private void Fanout04(int[] values) => SharedCallee(values);
    private void Fanout05(int[] values) => SharedCallee(values);
    private void Fanout06(int[] values) => SharedCallee(values);
    private void Fanout07(int[] values) => SharedCallee(values);
    private void Fanout08(int[] values) => SharedCallee(values);
    private void Fanout09(int[] values) => SharedCallee(values);
    private void Fanout10(int[] values) => SharedCallee(values);
    private void Fanout11(int[] values) => SharedCallee(values);
    private void Fanout12(int[] values) => SharedCallee(values);
    private void Fanout13(int[] values) => SharedCallee(values);
    private void Fanout14(int[] values) => SharedCallee(values);
    private void Fanout15(int[] values) => SharedCallee(values);
    private void Fanout16(int[] values) => SharedCallee(values);
    private void Fanout17(int[] values) => SharedCallee(values);
    private void Fanout18(int[] values) => SharedCallee(values);
    private void Fanout19(int[] values) => SharedCallee(values);
    private void Fanout20(int[] values) => SharedCallee(values);
    private void Fanout21(int[] values) => SharedCallee(values);
    private void Fanout22(int[] values) => SharedCallee(values);
    private void Fanout23(int[] values) => SharedCallee(values);
    private void Fanout24(int[] values) => SharedCallee(values);
    private void Fanout25(int[] values) => SharedCallee(values);
    private void Fanout26(int[] values) => SharedCallee(values);
    private void Fanout27(int[] values) => SharedCallee(values);
    private void Fanout28(int[] values) => SharedCallee(values);
    private void Fanout29(int[] values) => SharedCallee(values);
    private void Fanout30(int[] values) => SharedCallee(values);
    private void Fanout31(int[] values) => SharedCallee(values);
    private void Fanout32(int[] values) => SharedCallee(values);
    private void Fanout33(int[] values) => SharedCallee(values);

    private void CycleA(int[] values) => CycleB(values);
    private void CycleB(int[] values) => CycleA(values);
}
