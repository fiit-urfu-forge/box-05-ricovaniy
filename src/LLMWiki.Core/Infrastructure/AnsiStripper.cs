using System.Text;

namespace LLMWiki.Core.Infrastructure;

/// <summary>
/// Strips ANSI escape sequences (CSI, OSC, simple ESC sequences) and
/// non-printable control bytes from a stream of bytes coming out of a PTY,
/// so that the text can be displayed in a plain TextBox.
/// Implemented as a state machine for robustness against partial sequences.
/// </summary>
public static class AnsiStripper
{
    public static string Strip(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        var i = 0;

        while (i < input.Length)
        {
            var c = input[i];

            if (c == '\x1B')
            {
                i = SkipEscape(input, i + 1);
                continue;
            }

            if (IsAllowedControl(c) || !IsControl(c))
            {
                sb.Append(c);
            }

            i++;
        }

        return sb.ToString();
    }

    private static int SkipEscape(string input, int after)
    {
        if (after >= input.Length) return after;

        var next = input[after];

        // CSI: ESC [ params intermediate final
        if (next == '[')
        {
            var i = after + 1;
            while (i < input.Length && input[i] is >= '0' and <= '?') i++;
            while (i < input.Length && input[i] is >= ' ' and <= '/') i++;
            if (i < input.Length && input[i] is >= '@' and <= '~') i++;
            return i;
        }

        // OSC: ESC ] payload (BEL or ESC \)
        if (next == ']')
        {
            var i = after + 1;
            while (i < input.Length)
            {
                var ch = input[i];
                if (ch == '\x07') return i + 1;
                if (ch == '\x1B' && i + 1 < input.Length && input[i + 1] == '\\')
                    return i + 2;
                i++;
            }
            return i;
        }

        // single-char escape (Fe/Fs/Fp final byte)
        return after + 1;
    }

    private static bool IsControl(char c) =>
        c < 0x20 || c == 0x7F;

    private static bool IsAllowedControl(char c) =>
        c == '\n' || c == '\t';
}
