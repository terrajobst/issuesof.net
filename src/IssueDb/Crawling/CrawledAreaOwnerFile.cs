using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using IssueDb.Crawling;

namespace IssueDb;

public sealed class CrawledAreaOwnerFile
{
    public CrawledAreaOwnerFile(IEnumerable<CrawledAreaOwnerEntry> entries)
    {
        Entries = entries.ToDictionary(e => e.Area, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, CrawledAreaOwnerEntry> Entries { get; }

    public static async Task<CrawledAreaOwnerFile> GetAsync(string org, string repo)
    {
        var maxTries = 3;
        var jsonUrl = $"https://raw.githubusercontent.com/{org}/{repo}/main/docs/area-owners.json";
        var markdownUrl = $"https://raw.githubusercontent.com/{org}/{repo}/main/docs/area-owners.md";
        var jsonFileNotFound = false;

        while (maxTries-- > 0)
        {
            var client = new HttpClient();

            if (!jsonFileNotFound)
            {
                try
                {
                    // First attempt to load a JSON file
                    var jsonEntries = await client.GetFromJsonAsync<CrawledAreaOwnerJsonFile>(jsonUrl);
                    return new CrawledAreaOwnerFile(jsonEntries.Areas.Select(area => new CrawledAreaOwnerEntry(area.Label.Replace("area-", ""), area.Lead, area.Pod, area.Owners)));
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // If the JSON file was not found, fall back to trying to load the Markdown file
                    jsonFileNotFound = true;
                }
                catch (HttpRequestException ex)
                {
                    // This might be a transient error; log it and continue
                    Debug.WriteLine(ex);
                }
            }

            if (jsonFileNotFound)
            {
                try
                {
                    var contents = await client.GetStringAsync(markdownUrl);
                    return Parse(contents);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Neither the JSON nor MD files could be found; there's no area owner file
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    // This might be a transient error.
                    Debug.WriteLine(ex);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return null;
    }

    private static CrawledAreaOwnerFile Parse(string contents)
    {
        var lines = GetLines(contents);
        var entries = new List<CrawledAreaOwnerEntry>();

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length != 6)
                continue;

            var areaText = parts[1].Trim();
            var lead = GetUntaggedUserName(parts[2].Trim());
            var ownerText = parts[3].Trim();
            var owners = ownerText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(o => GetUntaggedUserName(o.Trim()))
                                  .ToArray();

            if (!TextTokenizer.TryParseArea(areaText, out var area))
                continue;

            var entry = new CrawledAreaOwnerEntry(area, lead, pod: null, owners);
            entries.Add(entry);
        }

        return new CrawledAreaOwnerFile(entries);

        static string GetUntaggedUserName(string userName)
        {
            return userName.StartsWith("@") ? userName[1..] : userName;
        }

        static IEnumerable<string> GetLines(string text)
        {
            using var stringReader = new StringReader(text);
            while (true)
            {
                var line = stringReader.ReadLine();
                if (line == null)
                    yield break;

                yield return line;
            }
        }
    }
}
