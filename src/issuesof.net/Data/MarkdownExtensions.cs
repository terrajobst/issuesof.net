
using Markdig;

using Microsoft.AspNetCore.Components;

namespace IssuesOfDotNet.Data
{
    public static class MarkdownExtensions
    {
        public static MarkupString AsInlineMarkdown(this string markup)
        {
            // We clip off the beginning and ending <p></p> tags. It's dirty,
            // but Alexandre gave it his blessing ;-)
            //
            // https://twitter.com/xoofx/status/1392198584733507596
            var html = Markdown.ToHtml(markup)[3..^5];
            return new MarkupString(html);
        }
    }
}
