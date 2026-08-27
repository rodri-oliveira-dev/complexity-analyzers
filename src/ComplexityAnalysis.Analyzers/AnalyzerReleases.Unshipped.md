### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
BIG0001 | Complexity | Info | Estimated algorithmic complexity.
BIG1001 | Complexity | Info | Linear lookup inside iteration.
BIG1002 | Complexity | Info | Materialization inside iteration.
BIG1003 | Complexity | Info | Ordering inside iteration.
BIG1004 | Complexity | Info | Input-dependent method call inside iteration.
BIG1005 | Complexity | Info | Exponential recursive growth.
BIG1006 | Complexity | Info | Method complexity exceeds configured threshold.
BIG2001 | Complexity | Info | Cyclomatic complexity exceeds configured threshold.
BIG2002 | Complexity | Info | Maximum nesting depth exceeds configured threshold.
BIG9000 | Infrastructure | Info | Analyzer execution probe.

### Changed Rules

Rule ID | Notes
--------|------
BIG0001 | Message now includes the method name and exposes `complexity` in diagnostic properties.
BIG1001 | Message now includes proven operation cost, iteration cost and composed contribution; diagnostic properties expose `operation`, `operationComplexity`, `iterationComplexity` and `combinedComplexity`.
BIG1002 | Message now includes proven materialization cost, iteration cost and composed contribution; diagnostic properties expose `operation`, `operationComplexity`, `iterationComplexity` and `combinedComplexity`.
BIG1003 | Message now includes proven consumed ordering cost, iteration cost and composed contribution; diagnostic properties expose `operation`, `operationComplexity`, `iterationComplexity` and `combinedComplexity`.
BIG1004 | Message now states the input-dependent callee cost and enclosing iteration; diagnostic properties expose `operation`, `operationComplexity`, `iterationComplexity` and `combinedComplexity`.
BIG1005 | Message now states exponential growth and exposes `complexity` plus `recurrenceClass`.
BIG1006 | Message wording was tightened and exposes `complexity` plus `threshold`.
BIG9000 | Diagnostic properties expose `diagnosticRole`.
BIG0001 | Estimated complexity can now report for supported constructors, accessors, operators, conversion operators, local functions, lambdas, anonymous methods and expression-bodied properties when complexity is known.
BIG1001-BIG1004 | Actionable diagnostics now respect executable-member body boundaries and can report inside supported non-method executable roots when the same evidence requirements are met.
BIG1005 | Direct-recursion reporting can now use the shared executable-member abstraction for supported non-lambda executable roots when recurrence extraction succeeds.
BIG1006 | Threshold diagnostics can now report for supported executable members when their known complexity exceeds the configured maximum.
BIG2001 | Added configurable Cyclomatic Complexity / Modified McCabe threshold reporting with `cyclomaticComplexity`, `threshold` and `cyclomaticComplexityMode` diagnostic properties.
BIG2002 | Added configurable Maximum Control-Flow Nesting Depth threshold reporting with `maximumNestingDepth` and `threshold` diagnostic properties.
