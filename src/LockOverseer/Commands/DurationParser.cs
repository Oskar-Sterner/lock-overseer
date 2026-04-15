namespace LockOverseer.Commands;

public static class DurationParser
{
    public static bool TryParse(string token, out int? minutes)
    {
        minutes = null;
        if (string.Equals(token, "perm", StringComparison.OrdinalIgnoreCase))
            return true;

        if (int.TryParse(token, out var m) && m > 0)
        {
            minutes = m;
            return true;
        }

        return false;
    }
}
