using System.Linq;

using IssuesOfDotNet.Data;
using IssuesOfDotNet.Querying;

using Microsoft.AspNetCore.Mvc;

namespace IssuesOfDotNet.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class CompletionController : Controller
    {
        private readonly CrawledIndexService _indexService;
        private CrawledIndexCompletionProvider _completionProvider;

        public CompletionController(CrawledIndexService indexService)
        {
            _indexService = indexService;            
        }

        [HttpGet]
        public CompletionResponse GetCompletions(string q, int pos)
        {
            var syntax = QuerySyntax.Parse(q);

            if (_completionProvider is null)
            {
                if (_indexService.Index is null)
                    return null;

                _completionProvider = new CrawledIndexCompletionProvider(_indexService.Index);
            }

            var result = _completionProvider.Complete(syntax, pos);

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
