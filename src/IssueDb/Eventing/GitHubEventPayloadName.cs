using System;
using System.Globalization;

namespace IssueDb.Eventing
{
    public sealed class GitHubEventPayloadName : IComparable, IComparable<GitHubEventPayloadName>
    {
        public GitHubEventPayloadName(string org, string repo, DateTimeOffset timestamp, string delivery)
        {
            Org = org;
            Repo = repo;
            Timestamp = timestamp;
            Delivery = delivery;
        }

        public string Org { get; }
        public string Repo { get; }
        public DateTimeOffset Timestamp { get; }
        public string Delivery { get; }

        public static GitHubEventPayloadName Parse(string value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            var firstSlash = value.IndexOf('/');
            if (firstSlash < 0)
                throw new FormatException();

            var secondSlash = value.IndexOf('/', firstSlash + 1);
            if (secondSlash < 0)
                throw new FormatException();

            var lastUnderscore = value.LastIndexOf('_');
            if (lastUnderscore < 0)
                throw new FormatException();

            var org = value.Substring(0, firstSlash);
            var repo = value.Substring(firstSlash + 1, secondSlash - firstSlash - 1);
            var timestampText = value.Substring(secondSlash + 1, lastUnderscore - secondSlash - 1);
            var delivery = value.Substring(lastUnderscore + 1);

            var timestampUtc = DateTime.ParseExact(timestampText, "yyyyMMdd_HHmmss", null, DateTimeStyles.AssumeUniversal);
            var timestamp = new DateTimeOffset(timestampUtc).ToLocalTime();
            return new GitHubEventPayloadName(org, repo, timestamp, delivery);
        }

        public int CompareTo(GitHubEventPayloadName other)
        {
            if (other is null)
                return 1;

            return Timestamp.CompareTo(other.Timestamp);
        }

        int IComparable.CompareTo(object obj)
        {
            return CompareTo(obj as GitHubEventPayloadName);
        }

        public override string ToString()
        {
            return $"{Org}/{Repo}/{Timestamp.ToUniversalTime():yyyyMMdd_HHmmss}_{Delivery}";
        }
    }
}
