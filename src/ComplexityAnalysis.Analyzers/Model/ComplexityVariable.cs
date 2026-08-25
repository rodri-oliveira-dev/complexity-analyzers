using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ComplexityVariable : IEquatable<ComplexityVariable>
{
    internal static readonly ComplexityVariable N = new("n");
    internal static readonly ComplexityVariable M = new("m");

    internal ComplexityVariable(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (!IsValidName(name))
        {
            throw new ArgumentException("Complexity variable names must start with an ASCII letter and contain only ASCII letters or digits.", nameof(name));
        }

        Name = name;
    }

    internal string Name
    {
        get;
    }

    public bool Equals(ComplexityVariable? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(Name, other.Name);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComplexityVariable);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Name);
    }

    public override string ToString()
    {
        return Name;
    }

    private static bool IsValidName(string name)
    {
        if (name.Length == 0 || !IsAsciiLetter(name[0]))
        {
            return false;
        }

        for (int index = 1; index < name.Length; index++)
        {
            char character = name[index];
            if (!IsAsciiLetter(character) && !IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char character)
    {
        return character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
    }

    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }
}
