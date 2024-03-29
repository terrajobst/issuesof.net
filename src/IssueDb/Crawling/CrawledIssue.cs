﻿using System.Text.Json.Serialization;

namespace IssueDb.Crawling;

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
    public CrawledRepo Repo { get; set; } = null!;
    public long Id { get; set; }
    public int Number { get; set; }
    public bool IsOpen { get; set; }
    public bool IsPullRequest { get; set; }
    public bool IsDraft { get; set; }
    public bool IsMerged { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int Comments { get; set; }
    public int ReactionsPlus1 { get; set; }
    public int ReactionsMinus1 { get; set; }
    public int ReactionsSmile { get; set; }
    public int ReactionsTada { get; set; }
    public int ReactionsThinkingFace { get; set; }
    public int ReactionsHeart { get; set; }
    public string[] Assignees { get; set; } = Array.Empty<string>();
    public CrawledLabel[] Labels { get; set; } = Array.Empty<CrawledLabel>();
    public CrawledMilestone? Milestone { get; set; }

    [JsonIgnore]
    public string Url => IsPullRequest
                            ? $"https://github.com/{Repo.Org}/{Repo.Name}/pull/{Number}"
                            : $"https://github.com/{Repo.Org}/{Repo.Name}/issues/{Number}";

    // Areas

    [JsonIgnore]
    public IEnumerable<string> Areas => Labels.SelectMany(l => TextTokenizer.GetAreaPaths(l.Name));

    [JsonIgnore]
    public IEnumerable<string> AreaNodes => Labels.SelectMany(l => TextTokenizer.GetAreaPaths(l.Name, segmentsOnly: true));

    [JsonIgnore]
    public IEnumerable<string> DirectAreaNodes => Labels.Where(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase))
                                                        .Select(l => l.Name.Substring(5));

    [JsonIgnore]
    public IEnumerable<string> AreaLeads => Labels.Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                  .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Leads)
                                                  .Where(m => !m.IsTeam)
                                                  .Select(m => m.UserName)
                                                  .Distinct(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public IEnumerable<string> AreaOwners => Labels.Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                   .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Owners)
                                                   .Where(m => !m.IsTeam)
                                                   .Select(m => m.UserName)
                                                   .Distinct(StringComparer.OrdinalIgnoreCase);

    // Operating Systems

    [JsonIgnore]
    public bool HasOwnedOperatingSystems => Labels.Where(l => l.Name.StartsWith("os-", StringComparison.OrdinalIgnoreCase))
                                                  .Any(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name));

    [JsonIgnore]
    public IEnumerable<string> OperatingSystems => Labels.Where(l => l.Name.StartsWith("os-", StringComparison.OrdinalIgnoreCase))
                                                         .Select(l => l.Name.Substring(3));

    [JsonIgnore]
    public IEnumerable<string> OperatingSystemLeads => Labels.Where(l => l.Name.StartsWith("os-", StringComparison.OrdinalIgnoreCase))
                                                             .Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                             .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Leads)
                                                             .Where(m => !m.IsTeam)
                                                             .Select(m => m.UserName)
                                                             .Distinct(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public IEnumerable<string> OperatingSystemOwners => Labels.Where(l => l.Name.StartsWith("os-", StringComparison.OrdinalIgnoreCase))
                                                              .Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                              .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Owners)
                                                              .Where(m => !m.IsTeam)
                                                              .Select(m => m.UserName)
                                                              .Distinct(StringComparer.OrdinalIgnoreCase);

    // Arch

    [JsonIgnore]
    public bool HasOwnedArchitectures => Labels.Where(l => l.Name.StartsWith("arch-", StringComparison.OrdinalIgnoreCase))
                                               .Any(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name));

    [JsonIgnore]
    public IEnumerable<string> Architectures => Labels.Where(l => l.Name.StartsWith("arch-", StringComparison.OrdinalIgnoreCase))
                                                      .Select(l => l.Name.Substring(5));

    [JsonIgnore]
    public IEnumerable<string> ArchitectureLeads => Labels.Where(l => l.Name.StartsWith("arch-", StringComparison.OrdinalIgnoreCase))
                                                          .Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                          .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Leads)
                                                          .Where(m => !m.IsTeam)
                                                          .Select(m => m.UserName)
                                                          .Distinct(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public IEnumerable<string> ArchitectureOwners => Labels.Where(l => l.Name.StartsWith("arch-", StringComparison.OrdinalIgnoreCase))
                                                           .Where(l => Repo.AreaOwnership.EntryByLabel.ContainsKey(l.Name))
                                                           .SelectMany(l => Repo.AreaOwnership.EntryByLabel[l.Name].Owners)
                                                           .Where(m => !m.IsTeam)
                                                           .Select(m => m.UserName)
                                                           .Distinct(StringComparer.OrdinalIgnoreCase);

    // Leads and Owners

    [JsonIgnore]
    public IEnumerable<string> Leads => HasOwnedArchitectures
                                            ? ArchitectureLeads
                                            : HasOwnedOperatingSystems
                                                ? OperatingSystemLeads
                                                : AreaLeads;

    [JsonIgnore]
    public IEnumerable<string> Owners => HasOwnedArchitectures
                                            ? ArchitectureOwners
                                            : HasOwnedOperatingSystems
                                                ? OperatingSystemOwners
                                                : AreaOwners;

    // Reactions

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
