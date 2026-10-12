namespace CafeMenuConverter.Core.Diagnostics;

public enum Severity
{
    Warning,
    Error,
}

public sealed record Diagnostic(Severity Severity, string Message, int? Page = null)
{
    public override string ToString()
    {
        var level = Severity == Severity.Error ? "Ошибка" : "Предупреждение";
        return Page is { } p ? $"{level} (стр. {p}): {Message}" : $"{level}: {Message}";
    }
}

public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = new();

    public IReadOnlyList<Diagnostic> Items => _items;

    public bool HasErrors => _items.Any(d => d.Severity == Severity.Error);

    public void Error(string message, int? page = null) => _items.Add(new Diagnostic(Severity.Error, message, page));

    public void Warning(string message, int? page = null) => _items.Add(new Diagnostic(Severity.Warning, message, page));
}
