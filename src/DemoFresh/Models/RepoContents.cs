namespace DemoFresh.Models;

public record RepoContents(
    string WorkingDirectory,
    string RepoUrl,
    string Branch,
    IReadOnlyList<RepoFile> Files);

public record RepoFile(
    string RelativePath,
    string Content,
    string Extension);
