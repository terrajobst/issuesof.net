
using System.Collections.Generic;
using System.Text;

using Markdig;
using Markdig.Extensions.Emoji;

using Microsoft.AspNetCore.Components;

namespace IssuesOfDotNet.Data
{
    public static class MarkdownExtensions
    {
        private static IDictionary<string, string> _emojiMapping = EmojiMapping.GetDefaultEmojiShortcodeToUnicode();

        public static MarkupString AsInlineMarkdown(this string markup)
        {
            // Escape any inline HTML first.
            markup = markup.Replace("<", "&lt;").Replace(">", "&gt;");

            // We clip off the beginning and ending <p></p> tags. It's dirty,
            // but Alexandre gave it his blessing ;-)
            //
            // https://twitter.com/xoofx/status/1392198584733507596
            var html = Markdown.ToHtml(markup)[3..^5];
            return new MarkupString(html);
        }

        public static string ExpandEmojis(this string text)
        {
            var sb = new StringBuilder(text);
            foreach (var (key, value) in _emojiMapping)
                sb.Replace(key, value);

            return sb.ToString();
        }
    }
}
