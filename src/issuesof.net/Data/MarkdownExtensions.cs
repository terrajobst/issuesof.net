
using System.Collections.Generic;
using System.Text;

using Markdig.Extensions.Emoji;

namespace IssuesOfDotNet.Data
{
    public static class MarkdownExtensions
    {
        private static IDictionary<string, string> _emojiMapping = EmojiMapping.GetDefaultEmojiShortcodeToUnicode();

        public static string HighlightCode(this string text)
        {
            // TODO: We should only expand balanced back ticks to <code></code>
            //       But we also need to escape HTML.

            return text;
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
