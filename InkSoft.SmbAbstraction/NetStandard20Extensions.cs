#if NETSTANDARD2_0
using System;
using System.Text.RegularExpressions;

namespace InkSoft.SmbAbstraction;

/// <summary>
/// TODO: Is there a canned nuget package that fills compatibility gaps for basic string operations in older .NET versions?
/// </summary>
public static class NetStandard20Extensions
{
    public static bool Contains(this string s, char value) => s.Contains(value.ToString());

    public static bool EndsWith(this string s, char value) => s.Length > 0 && s[s.Length - 1] == value;

    public static string Replace(this string s, string oldValue, string? newValue, StringComparison comparisonType) => comparisonType switch
    {
        StringComparison.OrdinalIgnoreCase
        or StringComparison.InvariantCultureIgnoreCase
        or StringComparison.CurrentCultureIgnoreCase
            => Regex.Replace(s, oldValue, newValue, RegexOptions.IgnoreCase),
        _
            => s.Replace(oldValue, newValue),
    };
}
#endif