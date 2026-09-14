using System.Collections.Generic;

namespace ComplexityAnalysis.Tool.Reporting;

internal sealed class ProjectReport
{
    public string SchemaVersion
    {
        get;
        init;
    } = "1.0";

    public bool Deterministic
    {
        get;
        init;
    } = true;

    public required string EntryPoint
    {
        get;
        init;
    }

    public List<AnalyzedProjectReport> Projects
    {
        get;
    } = [];

    public DuplicateSummaryReport Duplicates
    {
        get;
        set;
    } = DuplicateSummaryReport.Disabled;

    public List<QualityGateReport> QualityGates
    {
        get;
    } = [];
}

internal sealed class AnalyzedProjectReport
{
    public required string Name
    {
        get;
        init;
    }

    public required string Path
    {
        get;
        init;
    }

    public string Language
    {
        get;
        init;
    } = "C#";

    public List<AnalyzedFileReport> Files
    {
        get;
    } = [];

    public MetricReport<double> DuplicateRate
    {
        get;
        set;
    } = MetricReport<double>.Unknown();
}

internal sealed class AnalyzedFileReport
{
    public required string Path
    {
        get;
        init;
    }

    public int NormalizedTokenCount
    {
        get;
        set;
    }

    public int DuplicateTokenCount
    {
        get;
        set;
    }

    public MetricReport<double> DuplicateRate
    {
        get;
        set;
    } = MetricReport<double>.Unknown();

    public List<MemberReport> Members
    {
        get;
    } = [];
}

internal sealed class MemberReport
{
    public required string DisplayName
    {
        get;
        init;
    }

    public required string Kind
    {
        get;
        init;
    }

    public required string SymbolId
    {
        get;
        init;
    }

    public required SourceLocationReport Location
    {
        get;
        init;
    }

    public required BigOReport BigO
    {
        get;
        init;
    }

    public MetricReport<int> CyclomaticComplexity
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> MaximumNestingDepth
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> Nloc
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> StatementCount
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> TokenCount
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> ParameterCount
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public MetricReport<int> CognitiveComplexity
    {
        get;
        init;
    } = MetricReport<int>.Unknown();

    public HalsteadMetricReport Halstead
    {
        get;
        init;
    } = HalsteadMetricReport.Unknown();
}

internal sealed class BigOReport
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

    public bool IsUnknown
    {
        get;
        init;
    }

    internal static BigOReport Known(string value)
    {
        return new BigOReport
        {
            Status = "known",
            Value = value,
            IsUnknown = false,
        };
    }

    internal static BigOReport Unknown()
    {
        return new BigOReport
        {
            Status = "unknown",
            Value = null,
            IsUnknown = true,
        };
    }
}

internal sealed class MetricReport<T>
    where T : struct
{
    public required string Status
    {
        get;
        init;
    }

    public T? Value
    {
        get;
        init;
    }

    internal static MetricReport<T> Known(T value)
    {
        return new MetricReport<T>
        {
            Status = "known",
            Value = value,
        };
    }

    internal static MetricReport<T> Unknown()
    {
        return new MetricReport<T>
        {
            Status = "unknown",
            Value = null,
        };
    }
}

internal sealed class HalsteadMetricReport
{
    public required string Status
    {
        get;
        init;
    }

    public int? DistinctOperatorCount
    {
        get;
        init;
    }

    public int? DistinctOperandCount
    {
        get;
        init;
    }

    public int? TotalOperatorCount
    {
        get;
        init;
    }

    public int? TotalOperandCount
    {
        get;
        init;
    }

    public long? Vocabulary
    {
        get;
        init;
    }

    public long? Length
    {
        get;
        init;
    }

    public double? CalculatedLength
    {
        get;
        init;
    }

    public double? Volume
    {
        get;
        init;
    }

    public double? Difficulty
    {
        get;
        init;
    }

    public double? Effort
    {
        get;
        init;
    }

    public double? EstimatedImplementationTime
    {
        get;
        init;
    }

    public double? EstimatedDeliveredBugs
    {
        get;
        init;
    }

    internal static HalsteadMetricReport Unknown()
    {
        return new HalsteadMetricReport
        {
            Status = "unknown",
        };
    }
}

internal sealed class SourceLocationReport
{
    public required string FilePath
    {
        get;
        init;
    }

    public int StartLine
    {
        get;
        init;
    }

    public int StartColumn
    {
        get;
        init;
    }

    public int EndLine
    {
        get;
        init;
    }

    public int EndColumn
    {
        get;
        init;
    }

    public int Start
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

internal sealed class DuplicateSummaryReport
{
    internal static readonly DuplicateSummaryReport Disabled = new()
    {
        Enabled = false,
        MinimumTokens = 0,
        MinimumLines = null,
        TotalNormalizedTokens = 0,
        DuplicateTokenCount = 0,
        DuplicateRate = MetricReport<double>.Unknown(),
    };

    public bool Enabled
    {
        get;
        init;
    }

    public int MinimumTokens
    {
        get;
        init;
    }

    public int? MinimumLines
    {
        get;
        init;
    }

    public int TotalNormalizedTokens
    {
        get;
        init;
    }

    public int DuplicateTokenCount
    {
        get;
        init;
    }

    public MetricReport<double> DuplicateRate
    {
        get;
        init;
    } = MetricReport<double>.Unknown();

    public List<CloneGroupReport> CloneGroups
    {
        get;
    } = [];
}

internal sealed class CloneGroupReport
{
    public required string Id
    {
        get;
        init;
    }

    public int NormalizedTokenCount
    {
        get;
        init;
    }

    public int OccurrenceCount
    {
        get;
        init;
    }

    public int DuplicateTokenCount
    {
        get;
        init;
    }

    public MetricReport<double> DuplicatePercentage
    {
        get;
        init;
    } = MetricReport<double>.Unknown();

    public List<CloneOccurrenceReport> Occurrences
    {
        get;
    } = [];
}

internal sealed class CloneOccurrenceReport
{
    public required string ProjectPath
    {
        get;
        init;
    }

    public required string FilePath
    {
        get;
        init;
    }

    public required SourceLocationReport Location
    {
        get;
        init;
    }
}

internal sealed class QualityGateReport
{
    public required string Metric
    {
        get;
        init;
    }

    public required string Subject
    {
        get;
        init;
    }

    public required string Threshold
    {
        get;
        init;
    }

    public required string Actual
    {
        get;
        init;
    }

    public bool Passed
    {
        get;
        init;
    }
}
