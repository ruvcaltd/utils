using System;
using System.Collections.Generic;
using System.Text;

public class StreamingXmlParser
{
    private StringBuilder _buffer = new StringBuilder();
    private Stack<string> _tagStack = new Stack<string>();                       // supports nested tags if needed
    private Dictionary<string, StringBuilder> _inProgress = new Dictionary<string, StringBuilder>();
    private Dictionary<string, string> _values = new Dictionary<string, string>();

    // Snapshot of current values (call after ProcessChunk)
    public IReadOnlyDictionary<string, string> Values => _values;

    // Call this for every incoming chunk
    public Dictionary<string, string> ProcessChunk(string chunk)
    {
        _buffer.Append(chunk);

        while (true)
        {
            string buf = _buffer.ToString();

            // If no open tag on stack, look for an opening tag
            if (_tagStack.Count == 0)
            {
                int openIdx = buf.IndexOf('<');
                if (openIdx == -1) break; // no tag start
                int closeIdx = buf.IndexOf('>', openIdx + 1);
                if (closeIdx == -1) break; // incomplete tag, wait for more

                string tagInner = buf.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();

                if (tagInner.StartsWith("/"))
                {
                    // stray closing tag when none open — ignore it
                    _buffer.Remove(0, closeIdx + 1);
                    continue;
                }
                else
                {
                    // found opening tag -> create entry immediately
                    string tagName = tagInner;
                    _tagStack.Push(tagName);
                    if (!_inProgress.ContainsKey(tagName)) _inProgress[tagName] = new StringBuilder();
                    _values[tagName] = _inProgress[tagName].ToString(); // immediate presence (may be empty)
                    _buffer.Remove(0, closeIdx + 1);
                    continue;
                }
            }
            else // there is an open tag; append content up to next '<' only
            {
                string currentTag = _tagStack.Peek();
                int nextOpen = buf.IndexOf('<');

                if (nextOpen == -1)
                {
                    // whole buffer is content for current tag
                    _inProgress[currentTag].Append(buf);
                    _values[currentTag] = _inProgress[currentTag].ToString();
                    _buffer.Clear();
                    break;
                }

                // append everything up to the next '<' (may be zero chars)
                if (nextOpen > 0)
                {
                    _inProgress[currentTag].Append(buf.Substring(0, nextOpen));
                    _values[currentTag] = _inProgress[currentTag].ToString();
                    _buffer.Remove(0, nextOpen);
                    continue;
                }

                // buffer starts with '<' — check if we have a full tag (opening/closing)
                int closeIdx = buf.IndexOf('>');
                if (closeIdx == -1) break; // incomplete tag, wait for more chunks

                string tagInner = buf.Substring(1, closeIdx - 1).Trim();

                if (tagInner.StartsWith("/"))
                {
                    // closing tag
                    string closingName = tagInner.Substring(1);
                    if (_tagStack.Count > 0 && _tagStack.Peek() == closingName)
                    {
                        // normal close
                        _tagStack.Pop();
                        _buffer.Remove(0, closeIdx + 1);
                        continue;
                    }
                    else
                    {
                        // mismatch or closing deeper tag — try to recover by popping until match or ignoring
                        // (simple strategy: ignore unmatched closing)
                        _buffer.Remove(0, closeIdx + 1);
                        continue;
                    }
                }
                else
                {
                    // encountered another opening tag while one is open -> push it (supports nesting)
                    string newTag = tagInner;
                    _tagStack.Push(newTag);
                    if (!_inProgress.ContainsKey(newTag)) _inProgress[newTag] = new StringBuilder();
                    _values[newTag] = _inProgress[newTag].ToString();
                    _buffer.Remove(0, closeIdx + 1);
                    continue;
                }
            }
        }

        // return a snapshot copy
        return new Dictionary<string, string>(_values);
    }
}
