# Issue #15 SDD Slice 1 - C# Halstead Classification

## Specification

This specification defines the `ComplexityAnalysis.Analyzers` C# Halstead
classification convention, primitive-count model, derived metric model, and
internal executable-member integration. It does not add public diagnostics,
configuration, maintainability index calculation, or project-level aggregation.

Halstead metrics are independent from Big-O, Cyclomatic Complexity, Maximum
Control-Flow Nesting Depth, NLOC, statement count, token count, Parameter Count,
Cognitive Complexity, and any future maintainability index. No numerical
equivalence is claimed with Lizard, Visual Studio, or another implementation.

The primitive counts are:

```text
n1 = distinct operators
n2 = distinct operands
N1 = total operators
N2 = total operands
```

Derived metrics use the following project convention:

```text
vocabulary                  n = n1 + n2
length                      N = N1 + N2
calculated length           N^ = n1 * log2(n1) + n2 * log2(n2)
volume                      V = N * log2(n)
difficulty                  D = (n1 / 2) * (N2 / n2)
effort                      E = D * V
estimated implementation time T = E / 18
estimated delivered bugs    B = V / 3000
```

The implementation uses `double` for derived formulas and does not round
intermediate values. Text formatting of internal Halstead metric values uses
the invariant culture and the round-trip `G17` format so the same value has the
same representation regardless of machine locale. Future diagnostics may choose
their own centralized display precision without changing these internal values.

Primitive counts must be non-negative, and a distinct count cannot exceed its
matching total count. Degenerate formulas have explicit deterministic values:

- the `x * log2(x)` contribution used by calculated length is `0` when `x` is
  `0` or `1`; this preserves the limiting behavior for `0` and the exact
  logarithmic result for `1`;
- volume is `0` when vocabulary is `0` or `1`, because empty and single-symbol
  vocabularies carry no logarithmic information under this convention;
- difficulty is `0` when `n1 = 0` or `n2 = 0`, because there is no operator
  vocabulary or no operand vocabulary from which to compute the operand ratio;
- effort, implementation time, and delivered bugs are derived from those safe
  values and therefore remain finite and non-negative.

No public Halstead result may expose `NaN`, positive infinity, or negative
infinity.

The metric is source-based, deterministic, and C#-specific. Comments,
preprocessor directives, whitespace, formatting, and syntax trivia do not
contribute to any count.

## Executable-Member Ownership

Classification is scoped to one supported `ExecutableMember`.

The classifier must reuse the existing executable-member abstraction and body
ownership helpers:

- `ExecutableMember` supplies member identity, body, display name, diagnostic
  location, syntax tree, and supported member kind.
- `ExecutableMemberSyntax` supplies traversal that walks the current member's
  own body while skipping nested executable bodies.

Nested local functions, lambdas, anonymous methods, and other executable
constructs represented by the shared abstraction are independent metric roots.
A parent member must not include the complete body of a nested executable
member. The parent may still classify the nested executable declaration or
lambda header syntax that remains in the parent's owned body traversal, such as
the local function name, delegate/lambda creation, parameter identifiers, and
the `=>` token, because that syntax belongs to the containing member's
statement or expression.

Expression-bodied executable members count the body expression as the executable
body. The expression-body `=>` token is represented by the same
`LambdaOrExpressionBody` operator identity used for lambda arrows and must be
added by the classifier when the current executable root is expression-bodied.

## Operator Convention

An operator is a syntax construct that performs, selects, invokes, accesses,
creates, transfers control, or otherwise changes the meaning of executable C#
code. Operators are classified by `HalsteadOperatorKind`, not by raw token text.
Trivia and equivalent whitespace never affect operator identity.

Punctuation is not mechanically counted. Punctuation contributes only when it is
part of a meaningful C# operation listed below. Parentheses used only for
grouping, braces used only for blocks or initializers, commas, semicolons,
colons, attributes, modifiers, and separators do not count by themselves.

### Arithmetic And Unary Operators

Count each occurrence of:

- binary `+`, `-`, `*`, `/`, `%`;
- unary `+`, unary `-`, logical `!`, bitwise `~`;
- prefix and postfix `++` / `--`.

Prefix and postfix increment/decrement have distinct identities because C# gives
them different expression-result semantics.

### Comparison, Equality, Logical, And Bitwise Operators

Count each occurrence of:

- `==`, `!=`, `<`, `<=`, `>`, `>=`;
- short-circuit `&&`, `||`;
- bitwise/logical `&`, `|`, `^`;
- shift `<<`, `>>`, and unsigned right shift `>>>` when supported by the
  configured Roslyn/C# language version.

Relational patterns such as `> 0` use the corresponding comparison operator
identity.

### Assignment Operators

Count each occurrence of:

- simple assignment `=`;
- compound assignment `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`,
  `>>=`, and `>>>=` when supported;
- null-coalescing assignment `??=`.

Compound assignments are single operator occurrences. They are not decomposed
into assignment plus arithmetic/bitwise operators.

### Conditional And Null Operators

Count each occurrence of:

- conditional expression `?:` as `Conditional`;
- null-conditional member access `?.` as `ConditionalAccess`;
- null-conditional element access `?[]` as `ConditionalElementAccess`;
- null-coalescing `??` as `NullCoalescing`;
- null-coalescing assignment `??=` as `NullCoalescingAssignment`.

The `?` token in nullable type syntax, nullable annotations, or pattern syntax
is not counted as a Halstead operator unless it participates in one of the
operators above.

### Lambda, Expression Body, And Invocation

Count each occurrence of:

- lambda arrow `=>`;
- expression-body arrow `=>` on supported executable roots;
- switch-expression arm arrow `=>`;
- invocation syntax as `Invocation`.

The invoked method, local function, delegate, or callable member name is an
operand when it can be identified. Invocation parentheses are not counted as
punctuation outside the invocation operator.

### Patterns

Count each occurrence of:

- `is`;
- pattern combinators `not`, `and`, and `or`;
- switch `when` guards;
- relational-pattern operators such as `<`, `<=`, `>`, `>=`;
- list-pattern and slice-pattern syntax only when represented by one of the
  explicit listed operators, such as `CollectionSpread` or `Range`.

Declaration patterns, type patterns, property patterns, positional patterns,
constant patterns, var patterns, discard patterns, and recursive pattern shape
do not add punctuation operators by themselves. Their type names, constants,
property names, and pattern variables may be operands.

### Control Flow

The following C# control-flow constructs count as operators:

- `if`, `else`;
- `for`, `foreach`, `while`, `do`;
- `switch`, `case`, `default`, and switch-expression arms;
- `when` guards;
- `try`, `catch`, `finally`;
- `return`;
- `throw` statements and throw expressions;
- `await`;
- `yield return` and `yield break`;
- `break`, `continue`, `goto`, `goto case`, and `goto default`;
- `using`, `lock`, `fixed`, `checked`, and `unchecked` statements or
  expressions.

Control-flow operators are counted independently from Cyclomatic Complexity and
Cognitive Complexity. A construct can be a Halstead operator even when it is not
a decision point for another metric.

### Access, Creation, Range, Index, And Collections

Count each occurrence of:

- member access `.` as `MemberAccess`;
- element access `[]` as `ElementAccess`;
- object creation `new T(...)` as `ObjectCreation`;
- target-typed `new(...)` as `ImplicitObjectCreation`;
- array creation as `ArrayCreation`;
- collection expression `[...]` as `CollectionExpression`;
- collection expression spread `..expr` as `CollectionSpread`;
- range `..` as `Range`;
- index-from-end `^` as `Index`.

Initializer braces and collection separators are not counted by themselves. The
created type name and element/index/range expressions are classified through
operand rules.

## Operand Convention

An operand is a source-level value, symbolic reference, declared value name, or
type name that participates in executable code. Operands are classified by
`HalsteadOperandKind` plus a canonical value string.

When semantic information is already available and materially improves
correctness, the classifier should use Roslyn symbol identity to canonicalize
identifiers. It must not perform expensive project-wide semantic work, I/O, or
whole-solution scans. If semantic binding is inconclusive, a deterministic
syntax-based identity is acceptable.

### Identifiers And Symbols

Count identifier occurrences that participate in executable syntax:

- parameter references;
- local-variable declarations and references;
- local constants;
- fields and properties;
- methods, local functions, delegates, events, and callable member names when
  used as invocation, method-group, access, or event operands;
- pattern variables.

Repeated use of the same canonical identifier contributes multiple times to
`N2` and once to `n2`. Renaming an identifier may change operand identity but
must not change operator counts.

Member access uses both an operator and operands. For `customer.Name`, `.` is
`MemberAccess`; `customer` and `Name` are operands when they are represented by
source identifiers or resolvable symbols. For `customer?.Name`, `?.` is
`ConditionalAccess` with the same operand treatment.

Discard `_` is counted as a `Discard` operand only when it has semantic value in
the syntax being classified, such as a discard designation or discard pattern.
Unused trivia-like underscores outside such syntax do not exist in the token
stream and therefore cannot count.

### Literals

Literal identities are based on logical literal value rather than trivia or
surface spelling when Roslyn exposes that value cheaply:

- numeric literals use a canonical value that includes the constant value and
  effective primitive type, so `1`, `0x1`, and `1_0 - 9` are not conflated by
  text alone when individual literal tokens can be resolved;
- string literals and character literals use the decoded literal value;
- boolean literals use `bool:true` and `bool:false`;
- `null` uses the fixed identity `null`;
- constants use their symbolic identity when referenced and their initializer
  literal operands when the declaration is inside the owned body.

Different literal values have different operand identities. Repeated occurrences
of the same canonical literal value contribute multiple times to `N2` and once
to `n2`.

### Interpolated Strings

An interpolated string contributes:

- one string operand for the literal text content after escape normalization;
- operands/operators from each interpolation expression, classified recursively;
- an invocation/access/format operand only when the source syntax explicitly
  contains it.

Interpolation braces are not counted as punctuation operators. Alignment and
format clauses are classified only through their meaningful contained
expressions or literal values.

### Type Names

Type names count as operands when syntax uses them as meaningful executable
operands, including:

- object, array, stackalloc, default, `typeof`, `nameof`, `sizeof`, and cast
  syntax;
- declaration patterns and type patterns;
- explicit local declaration types inside the owned body;
- generic type names used in object creation, casts, or explicitly supplied
  type arguments.

The `var` keyword in local declarations is not itself a type-name operand. A
future classifier may use semantic type identity for `var` only if that improves
correctness without increasing traversal scope.

## Identity Rules

Operator identity is:

```text
HalsteadElementRole.Operator + HalsteadOperatorKind
```

Operand identity is:

```text
HalsteadElementRole.Operand + HalsteadOperandKind + canonical value
```

Identity comparison is ordinal and culture-invariant.

Deterministic examples:

- repeated `+` occurrences contribute multiple times to `N1` but once to `n1`;
- repeated use of the same canonical identifier contributes multiple times to
  `N2` but once to `n2`;
- renaming an identifier may change operand identity but must not change
  operator counts;
- different literal values have different operand identities;
- comments, trivia, whitespace, and equivalent formatting do not affect any
  identity;
- semantically equivalent qualifications such as `this.value` and `value`
  should share identity when Roslyn symbol identity is cheaply available;
- unresolved identifiers fall back to stable syntax text rather than causing
  nondeterministic identity.

## Domain Model

This slice adds only the internal representation needed by later classifier and
formula work:

- `HalsteadElementRole`;
- `HalsteadOperatorKind`;
- `HalsteadOperandKind`;
- `HalsteadElementIdentity`;
- `HalsteadElement`;
- `HalsteadClassificationResult`.

`HalsteadClassificationResult` stores the classified element sequence and
exposes the four primitive counts:

- `DistinctOperatorCount` (`n1`);
- `DistinctOperandCount` (`n2`);
- `TotalOperatorCount` (`N1`);
- `TotalOperandCount` (`N2`).

The model owns primitive counts and derived formulas. `HalsteadClassificationAnalyzer`
owns executable-member Roslyn classification. `HalsteadMetricsAnalyzer` is the
small integration layer that combines classification with formulas for callers
that need the final metric object.

## Public Threshold Decision

No public Halstead threshold diagnostic is introduced for issue #15.

The existing diagnostic philosophy requires actionable, evidence-backed rules.
Halstead values are useful as reproducible metric data, but common derived
values such as volume, difficulty, effort, implementation time, and estimated
bugs are sensitive to classification conventions and do not provide one
project-independent maintainability boundary that is clear enough for a public
diagnostic. A diagnostic added only for symmetry with other metrics would create
a noisy public contract without defensible guidance.

Consequences:

- no `BIG2xxx` ID is allocated for Halstead;
- no `complexity_analyzers.maximum_halstead_*` configuration key is added;
- `AnalyzerReleases.Unshipped.md` is unchanged for Halstead because no public
  diagnostic/configuration contract changed;
- Halstead remains an internal metric capability for future reporting or
  project-level tooling.

## Validation Plan

This slice requires:

- unit tests for primitive count aggregation and identity equality;
- unit tests proving the model can represent modern C# operator and operand
  identities required by the specification;
- `dotnet restore`;
- `dotnet build --configuration Release --no-restore`;
- focused Halstead model tests;
- broad `dotnet test --configuration Release --no-build`;
- `git diff --check`.
