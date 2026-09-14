using System.Collections.Generic;

namespace ComplexityAnalysis.Tool.Project;

internal sealed class CSharpProjectSource
{
    internal required string Name
    {
        get;
        init;
    }

    internal required string ProjectPath
    {
        get;
        init;
    }

    internal required string ProjectDirectory
    {
        get;
        init;
    }

    internal List<CSharpSourceFile> Files
    {
        get;
    } = [];
}

internal sealed class CSharpSourceFile
{
    internal required string Path
    {
        get;
        init;
    }

    internal required string Text
    {
        get;
        init;
    }
}
