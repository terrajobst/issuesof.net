﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace IssuesOfDotNet.Data;

public static class JavaScriptRegexExtensions
{
    public static string ToJavaScriptRegexAlternation(this IEnumerable<string> values)
    {
        return string.Join("|", values.OrderBy(x => x, Comparer<string>.Create(CompareStrings)));
    }

    private static int CompareStrings(string x, string y)
    {
        // NOTE: We want that "comments-asc" shows up before "comments"

        var result = StringComparer.OrdinalIgnoreCase.Compare(x, y);

        if (result != 0 && x is not null && y is not null)
        {
            if (x.StartsWith(y, StringComparison.OrdinalIgnoreCase) && x.Length > y.Length)
                return -1;

            if (y.StartsWith(x, StringComparison.OrdinalIgnoreCase) && y.Length > x.Length)
                return 1;
        }

        return result;
    }
}
