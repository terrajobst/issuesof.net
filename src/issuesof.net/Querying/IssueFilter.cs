﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;

namespace IssuesOfDotNet.Querying
{
    public sealed class IssueFilter
    {
        public bool? IsOpen { get; set; }
        public bool? IsPullRequest { get; set; }
        public bool? IsMerged { get; set; }
        public bool? NoAssignees { get; set; }
        public bool? NoLabels { get; set; }
        public bool? NoMilestone { get; set; }

        public string Author { get; set; }
        public string Milestone { get; set; }

        public List<string> IncludedOrgs { get; } = new List<string>();
        public List<string> IncludedRepos { get; } = new List<string>();
        public List<string> IncludedAssignees { get; } = new List<string>();
        public List<string> IncludedLabels { get; } = new List<string>();
        public List<string> IncludedTerms { get; } = new List<string>();

        public List<string> ExcludedOrgs { get; } = new List<string>();
        public List<string> ExcludedRepos { get; } = new List<string>();
        public List<string> ExcludedAssignees { get; } = new List<string>();
        public List<string> ExcludedAuthors { get; } = new List<string>();
        public List<string> ExcludedLabels { get; } = new List<string>();
        public List<string> ExcludedMilestones { get; } = new List<string>();
        public List<string> ExcludedTerms { get; } = new List<string>();

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
            AddBooleanFilter(lines, IsPullRequest, nameof(IsPullRequest));
            AddBooleanFilter(lines, IsMerged, nameof(IsMerged));
            AddBooleanFilter(lines, NoAssignees, nameof(NoAssignees));
            AddBooleanFilter(lines, NoLabels, nameof(NoLabels));
            AddBooleanFilter(lines, NoMilestone, nameof(NoMilestone));

            AddStringFilter(lines, Author, nameof(Author));
            AddStringFilter(lines, Milestone, nameof(Milestone));

            AddListFilter(lines, IncludedOrgs, nameof(IncludedOrgs));
            AddListFilter(lines, IncludedRepos, nameof(IncludedRepos));
            AddListFilter(lines, IncludedAssignees, nameof(IncludedAssignees));
            AddListFilter(lines, IncludedLabels, nameof(IncludedLabels));
            AddListFilter(lines, IncludedTerms, nameof(IncludedTerms));

            AddListFilter(lines, ExcludedOrgs, nameof(ExcludedOrgs));
            AddListFilter(lines, ExcludedRepos, nameof(ExcludedRepos));
            AddListFilter(lines, ExcludedAssignees, nameof(ExcludedAssignees));
            AddListFilter(lines, ExcludedAuthors, nameof(ExcludedAuthors));
            AddListFilter(lines, ExcludedLabels, nameof(ExcludedLabels));
            AddListFilter(lines, ExcludedMilestones, nameof(ExcludedMilestones));
            AddListFilter(lines, ExcludedTerms, nameof(ExcludedTerms));

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
        }

        public override string ToString()
        {
            using var writer = new StringWriter();
            WriteTo(writer);
            return writer.ToString();
        }
    }
}
