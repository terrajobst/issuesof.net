using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace IssuesOfDotNet.Pages
{
    public sealed partial class Stats
    {
        [Inject]
        public CrawledIndexService TrieService { get; set; }

        [Inject]
        public IWebHostEnvironment Environment { get; set; }

        public int NumberOfTrieNodes { get; set; }
        public int NumberOfTrieStringBytes { get; set; }
        public int NumberOfTrieBytes { get; set; }
        public bool IsDevelopment => Environment.IsDevelopment();

        protected override void OnInitialized()
        {
            if (IsDevelopment && TrieService.Index is not null)
                Calc(TrieService.Index.Trie.Root);

            void Calc(CrawledTrieNode node)
            {
                var nodeBytes = 16;
                var stringBytes = node.Text?.Length * 2 ?? 0;
                var childBytes = node.Children.Count * 8;
                var issueBytes = node.Issues.Count * 8;

                NumberOfTrieNodes++;
                NumberOfTrieStringBytes += stringBytes;
                NumberOfTrieBytes += nodeBytes + stringBytes + childBytes + issueBytes;

                foreach (var child in node.Children)
                    Calc(child);
            }
        }
    }
}
