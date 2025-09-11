
class StreamingXmlParser
{
    private StringBuilder _buffer = new StringBuilder();
    private string _currentTag = null;
    private Dictionary<string, StringBuilder> _inProgress = new Dictionary<string, StringBuilder>();
    private Dictionary<string, string> _values = new Dictionary<string, string>();

    public Dictionary<string, string> ProcessChunk(string chunk)
    {
        _buffer.Append(chunk);

        while (_buffer.Length > 0)
        {
            string text = _buffer.ToString();

            // If no tag is currently open, look for an opening tag
            if (_currentTag == null)
            {
                int openIndex = text.IndexOf('<');
                int closeIndex = text.IndexOf('>', openIndex + 1);

                if (openIndex == -1 || closeIndex == -1)
                    break; // incomplete tag, wait for more

                string tagName = text.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim('/');

                if (!tagName.StartsWith("/")) // opening tag
                {
                    _currentTag = tagName;
                    if (!_inProgress.ContainsKey(tagName))
                        _inProgress[tagName] = new StringBuilder();
                }

                // remove processed part
                _buffer.Remove(0, closeIndex + 1);
            }
            else
            {
                // Look for closing tag
                string closing = $"</{_currentTag}>";
                int closeIndex = text.IndexOf(closing);

                if (closeIndex == -1)
                {
                    // No closing tag yet, treat everything as content
                    _inProgress[_currentTag].Append(text);
                    _values[_currentTag] = _inProgress[_currentTag].ToString();

                    _buffer.Clear();
                    break;
                }
                else
                {
                    // Content up to closing tag
                    string content = text.Substring(0, closeIndex);
                    _inProgress[_currentTag].Append(content);
                    _values[_currentTag] = _inProgress[_currentTag].ToString();

                    // Remove processed
                    _buffer.Remove(0, closeIndex + closing.Length);

                    // Tag is closed
                    _currentTag = null;
                }
            }
        }

        return new Dictionary<string, string>(_values);
    }
}
