namespace LLMWiki.App.Pty;

public static class PtyTerminalFactory
{
    public static IPtyTerminal Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsConPty();
        return new UnixScriptPty();
    }
}
