using System.Linq;

using IssueDb.Querying.Syntax;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Mvc;

namespace IssuesOfDotNet.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class CompletionController : Controller
    {
        private readonly CompletionService _completionService;

        public CompletionController(CompletionService completionService)
        {
            _completionService = completionService;
        }

        [HttpGet]
        public CompletionResponse GetCompletions(string q, int pos)
        {
            var syntax = QuerySyntax.Parse(q ?? string.Empty);
            var result = _completionService.Provider.Complete(syntax, pos);

            if (result is null)
                return null;

            return new CompletionResponse
            {
                List = result.Completions.Take(50).ToArray(),
                From = result.Span.Start,
                To = result.Span.End
            };
        }

        public sealed class CompletionResponse
        {
            public string[] List { get; set; }
            public int From { get; set; }
            public int To { get; set; }
        }
    }
}
