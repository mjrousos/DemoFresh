namespace DemoFresh.Models;

public record Demo(
    string Name,
    string Description,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<string> FilePaths);
