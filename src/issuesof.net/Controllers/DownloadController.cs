using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;

using IssueDb.Crawling;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Mvc;

namespace IssuesOfDotNet.Controllers;

[Route("download")]
public class DownloadController
{
    private readonly SearchService _searchService;

    public DownloadController(SearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public IActionResult Get(string q)
    {
        var results = _searchService.Search(q);

        var memoryStream = new MemoryStream();

        using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("Org");
            writer.Write(",");
            writer.Write("Repo");
            writer.Write(",");
            writer.Write("Type");
            writer.Write(",");
            writer.Write("State");
            writer.Write(",");
            writer.Write("Number");
            writer.Write(",");
            writer.Write("Title");
            writer.Write(",");
            writer.Write("Comments");
            writer.Write(",");
            writer.Write("ReactionsPlus1");
            writer.Write(",");
            writer.Write("ReactionsMinus1");
            writer.Write(",");
            writer.Write("ReactionsSmile");
            writer.Write(",");
            writer.Write("ReactionsTada");
            writer.Write(",");
            writer.Write("ReactionsThinkingFace");
            writer.Write(",");
            writer.Write("ReactionsHeart");
            writer.Write(",");
            writer.Write("Reactions");
            writer.Write(",");
            writer.Write("Interactions");

            var sb = new StringBuilder();

            foreach (var key in results.GroupKeys)
            {
                writer.Write(",");
                writer.Write(Escape(sb, key.Group.ToString()));
            }

            writer.WriteLine();

            var groupValues = new string[results.GroupKeys.Count];

            foreach (var group in results.Roots)
            {
                var groupIndex = 0;
                Walk(group, groupValues, ref groupIndex, writer, sb);
            }
        }

        memoryStream.Position = 0;

        return new FileStreamResult(memoryStream, MediaTypeNames.Text.Plain)
        {
            FileDownloadName = "issues.csv"
        };

        static string Escape(StringBuilder sb, string text)
        {
            var needsEscaping = text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;

            if (!needsEscaping)
                return text;

            sb.Append('"');

            foreach (var c in text)
            {
                if (c == '"')
                    sb.Append('"');

                sb.Append(c);
            }

            sb.Append('"');

            var result = sb.ToString();
            sb.Clear();
            return result;
        }

        static string GetState(CrawledIssue issue)
        {
            if (issue.IsPullRequest && issue.IsMerged)
                return "merged";
            else if (issue.IsPullRequest && !issue.IsOpen)
                return "unmerged";
            else if (issue.IsOpen)
                return "open";
            else
                return "closed";
        }

        static void Walk(CrawledIssueOrGroup item, string[] groupValues, ref int groupIndex, TextWriter writer, StringBuilder sb)
        {
            if (item.IsGroup)
            {
                var group = item.ToGroup();
                groupValues[groupIndex] = group.Keys.Last();

                foreach (var child in group.Children)
                {
                    groupIndex++;
                    Walk(child, groupValues, ref groupIndex, writer, sb);
                    groupIndex--;
                }
            }
            else
            {
                var issue = item.ToIssue();

                writer.Write(Escape(sb, issue.Repo.Org));
                writer.Write(",");
                writer.Write(Escape(sb, issue.Repo.Name));
                writer.Write(",");
                writer.Write(issue.IsPullRequest ? "PR" : "Issue");
                writer.Write(",");
                writer.Write(GetState(issue));
                writer.Write(",");
                writer.Write(issue.Number);
                writer.Write(",");
                writer.Write(Escape(sb, issue.Title));
                writer.Write(",");
                writer.Write(issue.Comments);
                writer.Write(",");
                writer.Write(issue.ReactionsPlus1);
                writer.Write(",");
                writer.Write(issue.ReactionsMinus1);
                writer.Write(",");
                writer.Write(issue.ReactionsSmile);
                writer.Write(",");
                writer.Write(issue.ReactionsTada);
                writer.Write(",");
                writer.Write(issue.ReactionsThinkingFace);
                writer.Write(",");
                writer.Write(issue.ReactionsHeart);
                writer.Write(",");
                writer.Write(issue.Reactions);
                writer.Write(",");
                writer.Write(issue.Interactions);

                foreach (var groupValue in groupValues)
                {
                    writer.Write(",");
                    writer.Write(Escape(sb, groupValue));
                }

                writer.WriteLine();
            }
        }
    }
}
