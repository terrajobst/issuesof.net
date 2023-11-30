using System.CodeDom.Compiler;

using IssueDb.Querying.Ranges;

namespace IssueDb.Querying;

public sealed class IssueFilter
{
    public bool? IsOpen { get; set; }
    public bool? IsLocked { get; set; }
    public bool? IsPullRequest { get; set; }
    public bool? IsMerged { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsArchived { get; set; }
    public bool? NoAssignees { get; set; }
    public bool? NoLabels { get; set; }
    public bool? NoArea { get; set; }
    public bool? NoAreaLead { get; set; }
    public bool? NoAreaPod { get; set; }
    public bool? NoAreaOwner { get; set; }
    public bool? NoMilestone { get; set; }

    public string Author { get; set; }
    public string Milestone { get; set; }

    public List<string> IncludedOrgs { get; } = new List<string>();
    public List<string> IncludedRepos { get; } = new List<string>();
    public List<string> IncludedAssignees { get; } = new List<string>();
    public List<string> IncludedLabels { get; } = new List<string>();
    public List<string> IncludedAreas { get; } = new List<string>();
    public List<string> IncludedAreaNodes { get; } = new List<string>();
    public List<string> IncludedAreaLeads { get; } = new List<string>();
    public List<string> IncludedAreaPods { get; } = new List<string>();
    public List<string> IncludedAreaOwners { get; } = new List<string>();
    public List<string> IncludedTerms { get; } = new List<string>();

    public List<string> ExcludedOrgs { get; } = new List<string>();
    public List<string> ExcludedRepos { get; } = new List<string>();
    public List<string> ExcludedAssignees { get; } = new List<string>();
    public List<string> ExcludedAuthors { get; } = new List<string>();
    public List<string> ExcludedLabels { get; } = new List<string>();
    public List<string> ExcludedAreas { get; } = new List<string>();
    public List<string> ExcludedAreaNodes { get; } = new List<string>();
    public List<string> ExcludedAreaLeads { get; } = new List<string>();
    public List<string> ExcludedAreaPods { get; } = new List<string>();
    public List<string> ExcludedAreaOwners { get; } = new List<string>();
    public List<string> ExcludedMilestones { get; } = new List<string>();
    public List<string> ExcludedTerms { get; } = new List<string>();

    public RangeSyntax<DateTimeOffset> Created { get; set; }
    public RangeSyntax<DateTimeOffset> Updated { get; set; }
    public RangeSyntax<DateTimeOffset> Closed { get; set; }

    public RangeSyntax<int> Comments { get; set; }
    public RangeSyntax<int> Reactions { get; set; }
    public RangeSyntax<int> Interactions { get; set; }

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

        AddBooleanFilter(lines, IsOpen, nameof(IsOpen));
        AddBooleanFilter(lines, IsLocked, nameof(IsLocked));
        AddBooleanFilter(lines, IsPullRequest, nameof(IsPullRequest));
        AddBooleanFilter(lines, IsMerged, nameof(IsMerged));
        AddBooleanFilter(lines, IsDraft, nameof(IsDraft));
        AddBooleanFilter(lines, IsArchived, nameof(IsArchived));
        AddBooleanFilter(lines, NoAssignees, nameof(NoAssignees));
        AddBooleanFilter(lines, NoLabels, nameof(NoLabels));
        AddBooleanFilter(lines, NoArea, nameof(NoArea));
        AddBooleanFilter(lines, NoAreaLead, nameof(NoAreaLead));
        AddBooleanFilter(lines, NoAreaOwner, nameof(NoAreaOwner));
        AddBooleanFilter(lines, NoMilestone, nameof(NoMilestone));

        AddStringFilter(lines, Author, nameof(Author));
        AddStringFilter(lines, Milestone, nameof(Milestone));

        AddListFilter(lines, IncludedOrgs, nameof(IncludedOrgs));
        AddListFilter(lines, IncludedRepos, nameof(IncludedRepos));
        AddListFilter(lines, IncludedAssignees, nameof(IncludedAssignees));
        AddListFilter(lines, IncludedLabels, nameof(IncludedLabels));
        AddListFilter(lines, IncludedAreas, nameof(IncludedAreas));
        AddListFilter(lines, IncludedAreaNodes, nameof(IncludedAreaNodes));
        AddListFilter(lines, IncludedAreaLeads, nameof(IncludedAreaLeads));
        AddListFilter(lines, IncludedAreaOwners, nameof(IncludedAreaOwners));
        AddListFilter(lines, IncludedTerms, nameof(IncludedTerms));

        AddListFilter(lines, ExcludedOrgs, nameof(ExcludedOrgs));
        AddListFilter(lines, ExcludedRepos, nameof(ExcludedRepos));
        AddListFilter(lines, ExcludedAssignees, nameof(ExcludedAssignees));
        AddListFilter(lines, ExcludedAuthors, nameof(ExcludedAuthors));
        AddListFilter(lines, ExcludedLabels, nameof(ExcludedLabels));
        AddListFilter(lines, ExcludedAreas, nameof(ExcludedAreas));
        AddListFilter(lines, ExcludedAreaNodes, nameof(ExcludedAreaNodes));
        AddListFilter(lines, ExcludedAreaLeads, nameof(ExcludedAreaLeads));
        AddListFilter(lines, ExcludedAreaOwners, nameof(ExcludedAreaOwners));
        AddListFilter(lines, ExcludedMilestones, nameof(ExcludedMilestones));
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

        static void AddStringFilter(List<string> lines, string value, string name)
        {
            if (value is not null)
                lines.Add($"{name} = {value}");
        }

        static void AddListFilter(List<string> lines, List<string> value, string name)
        {
            if (value.Count > 0)
                lines.Add($"{name} = {string.Join(",", value)}");
        }

        static void AddRangeFilter<T>(List<string> lines, RangeSyntax<T> range, string name)
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
