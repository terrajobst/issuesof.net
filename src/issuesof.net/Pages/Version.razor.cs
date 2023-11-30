﻿using System.Reflection;

namespace IssuesOfDotNet.Pages;

public partial class Version
{
    private string _commit;

    protected override void OnInitialized()
    {
        var informationalVersion = GetType().Assembly.GetCustomAttributesData()
                                                     .Where(ca => ca.AttributeType == typeof(AssemblyInformationalVersionAttribute))
                                                     .SelectMany(ca => ca.ConstructorArguments.Select(a => a.Value as string))
                                                     .FirstOrDefault(string.Empty)!;

        var indexOfPlus = informationalVersion.IndexOf('+');
        _commit = indexOfPlus >= 0
                    ? informationalVersion.Substring(indexOfPlus + 1)
                    : null;
    }
}
