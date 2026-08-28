# Cognitive Complexity Convention

[English](cognitive-complexity.md) | [Portugues (Brasil)](../pt-BR/cognitive-complexity.md)

This document defines the `ComplexityAnalysis.Analyzers` C# Cognitive
Complexity convention. It is a documented project convention for estimating the
structural comprehension cost of one supported executable member. No exact
external-tool equivalence is claimed.

Cognitive Complexity is independent from Big-O, Cyclomatic Complexity, Maximum
Control-Flow Nesting Depth, NLOC, statement count, token count, Parameter Count,
Halstead metrics, and any future maintainability index. The values are not
combined into one score.

## Baseline

Straight-line executable code has Cognitive Complexity `0`.

```csharp
int Add(int left, int right)
{
    return left + right;
}
```

The expected score is `0`.

## Scoring Model

The calculator walks only the owned body of the current executable member.
Nested local functions, lambdas, and anonymous methods are executable-member
boundaries and are scored independently.

For each structural control-flow break:

```text
increment = 1 + current control-flow nesting
```

The current nesting starts at `0`. When a structural construct owns a nested
body, that body is visited at `current nesting + 1`. Sibling branches reuse the
same nesting value and do not accumulate each other's depth.

The score uses saturating integer addition.

## Structural Rules

| Construct | Structural increment | Nesting penalty |
| --- | --- | --- |
| `if` | `+1` | `+ current nesting` |
| `else if` | `+1` | `+ current nesting`; no artificial nesting for the chain |
| `else` | `+1` | None |
| `for` | `+1` | `+ current nesting` |
| `foreach` / `foreach var` | `+1` | `+ current nesting` |
| `while` | `+1` | `+ current nesting` |
| `do` | `+1` | `+ current nesting` |
| `switch` statement | `+1` for the switch family | `+ current nesting`; cases are sibling branches |
| switch expression | `+1` for the switch family | `+ current nesting`; arms are sibling branches |
| `catch` | `+1` per catch clause | `+ current nesting`; catches are siblings |
| `when` guard / catch filter | `+1` | `+ current nesting` where the guard appears |
| Conditional `?:` | `+1` | `+ current nesting` |
| Boolean `&&` / `||` sequence | `+1` for the first logical sequence, plus `+1` for each operator change | None |
| Pattern `and` / `or` sequence | `+1` for the first logical pattern sequence, plus `+1` for each operator change | None |
| Direct self-recursive call | `+1` once per member when proven by symbol identity | None |
| `break`, `continue`, `goto`, `goto case`, `goto default` | `+1` per statement | None |

## `if`, `else if`, And `else`

An `if` is a structural break. An `else if` is also a structural break, but it is
evaluated at the same nesting as the original `if`; the syntax shape does not
make the chain deeper by itself. A final `else` adds `1` because it is an
alternate branch, but it does not receive a nesting penalty.

Bodies inside the chain are still visited at one deeper nesting level.

## Loops

`for`, `foreach`, `foreach var`, `while`, and `do` each add `1 + current
nesting`. Their bodies are visited at one deeper nesting level. Conditions and
iteration expressions are visited at the current nesting and may add boolean
sequence cost.

## Switch

A `switch` statement contributes once for the switch family. `case` labels,
pattern labels, and `default` labels do not add by themselves. Each switch
section is a sibling branch. Control flow inside a section is visited at one
deeper nesting level.

A switch expression follows the same policy: the switch expression contributes
once, arms do not add by themselves, and each arm expression is visited at one
deeper nesting level.

Pattern labels and arms may still add pattern-sequence cost. `when` guards add a
guard increment.

## `try`, `catch`, And `finally`

`try` and `finally` do not add score by themselves. Each `catch` clause adds a
structural increment and its block is visited at one deeper nesting level.
Multiple catches are sibling branches. A catch filter adds the same guard cost
as `when`.

## Conditional Expressions

`condition ? whenTrue : whenFalse` adds `1 + current nesting`. The condition is
visited at the current nesting. Both result expressions are visited at one
deeper nesting level, so nested ternaries receive local nesting penalties.

## Boolean Sequences

Short-circuit boolean chains are counted as comprehension breaks inside
conditions and expressions:

| Expression | Boolean sequence cost |
| --- | --- |
| `a && b` | `1` |
| `a && b && c` | `1` |
| `a || b || c` | `1` |
| `a && b || c` | `2` |
| `(a && b) || (c && d)` | `3` |

Parentheses alone do not add score. They only matter when the underlying
Roslyn structure changes the encountered logical operator sequence.

## Patterns And Guards

Pattern `and` and `or` chains use the same sequence policy as `&&` and `||`.
`not`, relational patterns, property patterns, list patterns, declaration
patterns, constant patterns, discard patterns, and var patterns do not add by
themselves, though their nested subpatterns are still inspected.

`when` guards on switch labels and switch expression arms, and catch filters,
add `1 + current nesting`. The guard expression is then inspected for boolean
sequence cost.

## Recursion

Direct self-recursion adds `1` once per executable member when the call target is
proven with Roslyn symbol identity. The calculator does not use textual method
names and does not perform project-wide call-graph analysis. Mutual recursion is
outside the Cognitive Complexity convention.

Lambda and anonymous-method roots do not currently participate in direct
self-recursion scoring.

## Jumps And Exclusions

`break`, `continue`, and `goto` statements add `1` because they interrupt the
local linear flow. `goto case` and `goto default` use the same rule.

The following constructs are deliberately excluded unless they contain another
counted construct:

- `return`;
- `throw` statements and throw expressions;
- `await`;
- `yield return` and `yield break`;
- `lock`, `using`, `fixed`, `checked`, and `unchecked`;
- plain lexical blocks;
- object, collection, array, property, and anonymous-object initializers;
- member access, invocation, assignment, arithmetic, null-coalescing, and
  null-conditional expressions;
- comments and whitespace.

## Threshold And Diagnostics

`complexity_analyzers.maximum_cognitive_complexity` is an opt-in non-negative
integer threshold. Missing or invalid configuration leaves the threshold unset
and produces no diagnostic.

`BIG2007` reports only when:

- the executable member is supported and has a body;
- the threshold is configured with a valid non-negative integer;
- the actual Cognitive Complexity is strictly greater than the threshold.

Below-threshold and equal-to-threshold values do not report.

The diagnostic location is the stable executable-member location. Diagnostic
properties include `cognitiveComplexity` and `threshold`.

## Worked Example

```csharp
void M(bool a, bool b, bool c)
{
    if (a)
    {
        while (b)
        {
            if (c)
            {
            }
        }
    }
}
```

| Construct | Base increment | Nesting increment | Subtotal |
| --- | --- | --- | --- |
| outer `if` at nesting `0` | `1` | `0` | `1` |
| `while` at nesting `1` | `1` | `1` | `2` |
| inner `if` at nesting `2` | `1` | `2` | `3` |
| Total |  |  | `6` |

Flat sibling decisions are cheaper:

```csharp
if (a) {}
if (b) {}
if (c) {}
```

Each `if` is at nesting `0`, so the total is `3`. The nested example above is
greater because local nesting increases comprehension cost.

## Limitations

Cognitive Complexity does not measure runtime complexity, memory complexity,
domain quality, subjective readability, formatting style, Halstead volume, or
API design quality. It is not automatically equivalent to any external tool and
does not prescribe an automatic refactoring.
