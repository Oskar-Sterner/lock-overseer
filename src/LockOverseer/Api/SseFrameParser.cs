using System.Collections.Generic;
using System.Text;

namespace LockOverseer.Api;

public sealed class SseFrameParser
{
    private readonly StringBuilder _lineBuffer = new();
    private long? _currentId;
    private string? _currentEvent;
    private readonly StringBuilder _currentData = new();
    private bool _hasContent;

    public void Feed(string chunk, List<SseFrame> out_)
    {
        foreach (var ch in chunk)
        {
            if (ch == '\n')
            {
                HandleLine(_lineBuffer.ToString(), out_);
                _lineBuffer.Clear();
            }
            else if (ch != '\r')
            {
                _lineBuffer.Append(ch);
            }
        }
    }

    private void HandleLine(string line, List<SseFrame> out_)
    {
        if (line.Length == 0)
        {
            if (_hasContent && _currentEvent is not null)
                out_.Add(new SseFrame(_currentId, _currentEvent, _currentData.ToString()));
            _currentId = null;
            _currentEvent = null;
            _currentData.Clear();
            _hasContent = false;
            return;
        }
        if (line[0] == ':') return;

        var colon = line.IndexOf(':');
        string field, value;
        if (colon < 0) { field = line; value = ""; }
        else
        {
            field = line.Substring(0, colon);
            value = colon + 1 < line.Length && line[colon + 1] == ' '
                ? line.Substring(colon + 2)
                : line.Substring(colon + 1);
        }

        switch (field)
        {
            case "id":
                if (long.TryParse(value, out var parsed)) _currentId = parsed;
                _hasContent = true;
                break;
            case "event":
                _currentEvent = value;
                _hasContent = true;
                break;
            case "data":
                if (_currentData.Length > 0) _currentData.Append('\n');
                _currentData.Append(value);
                _hasContent = true;
                break;
            default: break;
        }
    }
}
