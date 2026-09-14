namespace ComplexityAnalysis.Tool.Reporting;

internal interface IReportWriter
{
    string Write(ProjectReport report);
}
