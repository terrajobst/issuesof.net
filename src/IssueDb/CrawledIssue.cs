using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace IssuesOfDotNet
{
    // TODO: We should record whether an issue was deleted or transferred
    //
    //       Maybe we can use the GraphQL APIs to make this more palatable to query,
    //       rather than having to fetch the events for every single issue.
    //
    //       But before we do any of that, we should create a temp repo and see
    //       what actually happens.
    //
    // TODO: We should pre-process the title's markdown into HTML (for faster display)

    public sealed class CrawledIssue
    {
        public CrawledRepo Repo { get; set; }
        public int Number { get; set; }
        public bool IsOpen { get; set; }
        public bool IsPullRequest { get; set; }
        public bool IsDraft { get; set; }
        public bool IsMerged { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public string CreatedBy { get; set; }
        public bool IsLocked { get; set; }
        public int Comments { get; set; }
        public int ReactionsPlus1 { get; set; }
        public int ReactionsMinus1 { get; set; }
        public int ReactionsSmile { get; set; }
        public int ReactionsTada { get; set; }
        public int ReactionsThinkingFace { get; set; }
        public int ReactionsHeart { get; set; }
        public string[] Assignees { get; set; }
        public CrawledLabel[] Labels { get; set; }
        public CrawledMilestone Milestone { get; set; }

        [JsonIgnore]
        public string Url => IsPullRequest
                                ? $"https://github.com/{Repo.Org}/{Repo.Name}/issues/{Number}"
                                : $"https://github.com/{Repo.Org}/{Repo.Name}/pull/{Number}";

        [JsonIgnore]
        public IEnumerable<string> Areas => Labels.SelectMany(l => TextTokenizer.GetAreaPaths(l.Name));

        [JsonIgnore]
        public IEnumerable<string> AreaNodes => Labels.SelectMany(l => TextTokenizer.GetAreaPaths(l.Name, segmentsOnly: true));

        [JsonIgnore]
        public int Reactions => ReactionsPlus1
                                + ReactionsMinus1
                                + ReactionsSmile
                                + ReactionsTada
                                + ReactionsThinkingFace
                                + ReactionsHeart;

        [JsonIgnore]
        public int Interactions => Comments + Reactions;
    }
}
