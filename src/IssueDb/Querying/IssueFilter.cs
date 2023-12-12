using System.CodeDom.Compiler;

using IssueDb.Querying.Ranges;

namespace IssueDb.Querying;

public sealed class IssueFilter
{
    public bool? IsLocked { get; set; }
    public bool? IsPullRequest { get; set; }
    public bool? IsArchived { get; set; }
    public bool? NoAssignees { get; set; }
    public bool? NoLabels { get; set; }
    public bool? NoArea { get; set; }
    public bool? NoAreaLead { get; set; }
    public bool? NoAreaOwner { get; set; }
    public bool? NoOperatingSystem { get; set; }
    public bool? NoOperatingSystemLead { get; set; }
    public bool? NoOperatingSystemOwner { get; set; }
    public bool? NoArchitecture { get; set; }
    public bool? NoArchitectureLead { get; set; }
    public bool? NoArchitectureOwner { get; set; }
    public bool? NoLead { get; set; }
    public bool? NoOwner { get; set; }
    public bool? NoMilestone { get; set; }

    public List<string> IncludedTerms { get; } = new List<string>();
    public List<string> ExcludedTerms { get; } = new List<string>();

    public RangeSyntax<DateTimeOffset>? Created { get; set; }
    public RangeSyntax<DateTimeOffset>? Updated { get; set; }
    public RangeSyntax<DateTimeOffset>? Closed { get; set; }

    public RangeSyntax<int>? Comments { get; set; }
    public RangeSyntax<int>? Reactions { get; set; }
    public RangeSyntax<int>? Interactions { get; set; }

    public List<IssueSort> Sort { get; } = new List<IssueSort>();
    public List<IssueGroup> Groups { get; } = new List<IssueGroup>();
    public List<IssueGroupSort> GroupSort { get; } = new List<IssueGroupSort>();

    public void WriteTo(TextWriter writer)
    {
        if (writer is IndentedTextWriter indentedTextWriter)
        {
            WriteTo(indentedTextWriter);
        }
        else
        {
            indentedTextWriter = new IndentedTextWriter(writer);
            WriteTo(indentedTextWriter);
        }
    }

    private void WriteTo(IndentedTextWriter writer)
    {
        var lines = new List<string>();

        AddBooleanFilter(lines, IsLocked, nameof(IsLocked));
        AddBooleanFilter(lines, IsPullRequest, nameof(IsPullRequest));
        AddBooleanFilter(lines, IsArchived, nameof(IsArchived));
        AddBooleanFilter(lines, NoAssignees, nameof(NoAssignees));
        AddBooleanFilter(lines, NoLabels, nameof(NoLabels));
        AddBooleanFilter(lines, NoArea, nameof(NoArea));
        AddBooleanFilter(lines, NoAreaLead, nameof(NoAreaLead));
        AddBooleanFilter(lines, NoAreaOwner, nameof(NoAreaOwner));
        AddBooleanFilter(lines, NoOperatingSystem, nameof(NoOperatingSystem));
        AddBooleanFilter(lines, NoOperatingSystemLead, nameof(NoOperatingSystemLead));
        AddBooleanFilter(lines, NoOperatingSystemOwner, nameof(NoOperatingSystemOwner));
        AddBooleanFilter(lines, NoArchitecture, nameof(NoArchitecture));
        AddBooleanFilter(lines, NoArchitectureLead, nameof(NoArchitectureLead));
        AddBooleanFilter(lines, NoArchitectureOwner, nameof(NoArchitectureOwner));
        AddBooleanFilter(lines, NoLead, nameof(NoLead));
        AddBooleanFilter(lines, NoOwner, nameof(NoOwner));
        AddBooleanFilter(lines, NoMilestone, nameof(NoMilestone));

        AddListFilter(lines, IncludedTerms, nameof(IncludedTerms));
        AddListFilter(lines, ExcludedTerms, nameof(ExcludedTerms));

        AddRangeFilter(lines, Created, nameof(Created));
        AddRangeFilter(lines, Updated, nameof(Updated));
        AddRangeFilter(lines, Closed, nameof(Closed));

        AddRangeFilter(lines, Comments, nameof(Comments));
        AddRangeFilter(lines, Reactions, nameof(Reactions));
        AddRangeFilter(lines, Interactions, nameof(Interactions));

        if (lines.Count > 1)
        {
            writer.WriteLine("AND");
            writer.Indent++;
        }

        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }

        if (lines.Count > 1)
        {
            writer.Indent--;
        }

        if (Sort.Count > 0)
        {
            writer.WriteLine("ORDER BY");
            writer.Indent++;
            foreach (var sort in Sort)
                writer.WriteLine(sort);
            writer.Indent--;
        }

        if (Groups.Count > 0)
        {
            writer.WriteLine("GROUP BY");
            writer.Indent++;
            foreach (var group in Groups)
                writer.WriteLine(group);
            writer.Indent--;
        }

        if (GroupSort.Count > 0)
        {
            writer.WriteLine("ORDER GROUPS BY");
            writer.Indent++;
            foreach (var sort in GroupSort)
                writer.WriteLine(sort);
            writer.Indent--;
        }

        static void AddBooleanFilter(List<string> lines, bool? value, string name)
        {
            if (value is not null)
                lines.Add($"{name} = {value}");
        }

        static void AddListFilter(List<string> lines, List<string> value, string name)
        {
            if (value.Count > 0)
                lines.Add($"{name} = {string.Join(",", value)}");
        }

        static void AddRangeFilter<T>(List<string> lines, RangeSyntax<T>? range, string name)
            where T : IComparable<T>
        {
            if (range is not null)
                lines.Add($"{name} {range}");
        }
    }

    public override string ToString()
    {
        using var writer = new StringWriter();
        WriteTo(writer);
        return writer.ToString();
    }
}
