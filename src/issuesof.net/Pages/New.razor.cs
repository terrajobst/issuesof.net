namespace IssuesOfDotNet.Pages;

public partial class New
{
    private string _filter;

    protected override void OnInitialized()
    {
        RepoEntries = GetRepoEntries();
        UpdateIsVisible();
    }

    private string Filter
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                UpdateIsVisible();
            }
        }
    }

    private IEnumerable<RepoEntry> FilteredRepoEntries => RepoEntries.Where(r => r.IsVisible);

    private List<RepoEntry> RepoEntries { get; set; }

    private sealed class RepoEntry
    {
        public bool IsVisible { get; set; }
        public int Indent { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        public RepoEntry[] Ancestors { get; set; } = Array.Empty<RepoEntry>();
    }

    private void UpdateIsVisible()
    {
        foreach (var entry in RepoEntries)
        {
            entry.IsVisible = IsVisible(entry);
            if (entry.IsVisible)
            {
                foreach (var ancestor in entry.Ancestors)
                    ancestor.IsVisible = true;
            }
        }
    }

    private bool IsVisible(RepoEntry repo)
    {
        if (string.IsNullOrEmpty(Filter))
            return true;

        return repo.Name?.Contains(Filter, StringComparison.OrdinalIgnoreCase) == true ||
               repo.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) == true ||
               repo.Ancestors.Any(IsVisible);
    }

    private List<RepoEntry> GetRepoEntries()
    {
        var text = GetRepoEntriesText();
        return ParseRepoEntries(text);
    }

    private string GetRepoEntriesText()
    {
        using var stream = GetType().Assembly.GetManifestResourceStream(GetType().FullName + ".razor.txt");
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    private static List<RepoEntry> ParseRepoEntries(string text)
    {
        using var stringReader = new StringReader(text);

        var result = new List<RepoEntry>();

        var indentStack = new Stack<int>();
        indentStack.Push(0);

        while (stringReader.ReadLine() is string line)
        {
            if (line.Trim().Length == 0)
                continue;

            var lineTrimmedStart = line.TrimStart();
            var lineIndent = line.Length - lineTrimmedStart.Length;

            while (indentStack.Peek() > lineIndent)
                indentStack.Pop();

            if (lineIndent > indentStack.Peek())
                indentStack.Push(lineIndent);

            var indent = indentStack.Count - 1;
            var startOfDescription = lineTrimmedStart.IndexOf("  ");
            if (startOfDescription < 0)
            {
                result.Add(new RepoEntry
                {
                    Indent = indent,
                    Name = lineTrimmedStart.TrimEnd()
                });
            }
            else
            {
                var repo = lineTrimmedStart.Substring(0, startOfDescription);
                var link = GetRepoLink(repo);
                var description = lineTrimmedStart.Substring(startOfDescription).Trim();

                result.Add(new RepoEntry
                {
                    Indent = indent,
                    Name = repo,
                    Link = link,
                    Description = description,
                });
            }
        }

        var ancestorStack = new Stack<RepoEntry>();

        foreach (var entry in result)
        {
            while (ancestorStack.Count > entry.Indent)
                ancestorStack.Pop();

            entry.Ancestors = ancestorStack.ToArray();

            if (entry.Indent + 1 > ancestorStack.Count)
                ancestorStack.Push(entry);
        }

        return result;
    }

    private static string GetRepoLink(string repo)
    {
        if (repo == "Dev Community")
            return "https://developercommunity.visualstudio.com/search?space=61";

        return $"https://github.com/{repo}";
    }
}
