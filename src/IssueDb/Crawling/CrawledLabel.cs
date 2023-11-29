using System.Drawing;
using System.Globalization;
using System.Text.Json.Serialization;

namespace IssueDb.Crawling;

public sealed class CrawledLabel
{
    private Color? _color;

    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    [JsonPropertyName("BackgroundColor")]
    public string ColorText { get; set; }

    [JsonIgnore]
    public Color Color
    {
        get
        {
            if (_color is null)
                _color = ParseColor(ColorText);

            return _color.Value;
        }
    }

    private static Color ParseColor(string color)
    {
        if (!string.IsNullOrEmpty(color) && color.Length == 6 &&
            int.TryParse(color[0..2], NumberStyles.HexNumber, null, out var r) &&
            int.TryParse(color[2..4], NumberStyles.HexNumber, null, out var g) &&
            int.TryParse(color[4..6], NumberStyles.HexNumber, null, out var b))
        {
            return Color.FromArgb(r, g, b);
        }

        return Color.Black;
    }
}
