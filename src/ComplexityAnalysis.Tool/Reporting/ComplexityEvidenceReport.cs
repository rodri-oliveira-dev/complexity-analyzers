using System.Collections.Generic;

namespace ComplexityAnalysis.Tool.Reporting;

internal sealed class ComplexityEvidenceReport
{
    public bool IsTruncated
    {
        get;
        init;
    }

    public int MaximumDepth
    {
        get;
        init;
    }

    public int MaximumSteps
    {
        get;
        init;
    }

    public string? UnknownReason
    {
        get;
        init;
    }

    public List<ComplexityEvidenceStepReport> Steps
    {
        get;
    } = [];
}

internal sealed class ComplexityEvidenceStepReport
{
    public required string Identity
    {
        get;
        init;
    }

    public required string Kind
    {
        get;
        init;
    }

    public required string Origin
    {
        get;
        init;
    }

    public required string Operation
    {
        get;
        init;
    }

    public required ComplexityEvidenceLocationReport Location
    {
        get;
        init;
    }

    public int Depth
    {
        get;
        init;
    }

    public List<string> Dimensions
    {
        get;
    } = [];

    public required ComplexityEstimateReport Estimate
    {
        get;
        init;
    }

    public string? Composition
    {
        get;
        init;
    }

    public string? UnknownReason
    {
        get;
        init;
    }
}

internal sealed class ComplexityEvidenceLocationReport
{
    public int RelativeStart
    {
        get;
        init;
    }

    public int Length
    {
        get;
        init;
    }
}

internal sealed class ComplexityEstimateReport
{
    public required string Status
    {
        get;
        init;
    }

    public string? Value
    {
        get;
        init;
    }

    public required string Guarantee
    {
        get;
        init;
    }

    public List<ComplexityAssumptionReport> Assumptions
    {
        get;
    } = [];
}

internal sealed class ComplexityAssumptionReport
{
    public required string Kind
    {
        get;
        init;
    }

    public required string Description
    {
        get;
        init;
    }
}
