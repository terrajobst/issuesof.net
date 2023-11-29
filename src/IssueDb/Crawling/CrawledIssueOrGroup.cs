namespace IssueDb.Crawling;

public struct CrawledIssueOrGroup : IEquatable<CrawledIssueOrGroup>
{
    private readonly object _value;

    public CrawledIssueOrGroup(CrawledIssue issue)
    {
        _value = issue;
    }

    public CrawledIssueOrGroup(CrawledIssueGroup group)
    {
        _value = group;
    }

    public bool IsIssue => _value is CrawledIssue;

    public bool IsGroup => _value is CrawledIssueGroup;

    public CrawledIssue ToIssue()
    {
        return _value is CrawledIssue issue ? issue : throw new InvalidOperationException("Not an issue");
    }

    public CrawledIssueGroup ToGroup()
    {
        return _value is CrawledIssueGroup group ? group : throw new InvalidOperationException("Not a group");
    }

    public override bool Equals(object obj)
    {
        return obj is CrawledIssueOrGroup other && Equals(other);
    }

    public bool Equals(CrawledIssueOrGroup other)
    {
        return ReferenceEquals(_value, other._value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_value);
    }

    public static implicit operator CrawledIssueOrGroup(CrawledIssue issue) => new(issue);

    public static implicit operator CrawledIssueOrGroup(CrawledIssueGroup group) => new(group);

    public static bool operator ==(CrawledIssueOrGroup left, CrawledIssueOrGroup right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CrawledIssueOrGroup left, CrawledIssueOrGroup right)
    {
        return !(left == right);
    }
}
