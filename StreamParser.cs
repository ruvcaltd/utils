class StreamParser
{
    private StringBuilder _buffer = new StringBuilder();
    private Dictionary<string, string> _values = new Dictionary<string, string>();

    // Regex for <Tag>content</Tag>
    private Regex _tagRegex = new Regex(@"<(?<tag>\w+)>(?<content>.*?)</\k<tag>>",
                                        RegexOptions.Singleline | RegexOptions.Compiled);

    public Dictionary<string, string> ProcessChunk(string chunk)
    {
        _buffer.Append(chunk);

        string current = _buffer.ToString();
        var matches = _tagRegex.Matches(current);

        foreach (Match match in matches)
        {
            string tag = match.Groups["tag"].Value;
            string content = match.Groups["content"].Value;

            _values[tag] = content; // update dictionary as it streams
        }

        // Optional: trim processed part from buffer if tags are fully closed
        if (matches.Count > 0)
        {
            var lastMatch = matches[matches.Count - 1];
            _buffer.Remove(0, lastMatch.Index + lastMatch.Length);
        }

        return new Dictionary<string, string>(_values); // return snapshot
    }
}
