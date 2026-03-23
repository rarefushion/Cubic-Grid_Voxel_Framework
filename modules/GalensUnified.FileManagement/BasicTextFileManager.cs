
using System.Runtime.CompilerServices;
using System.Text;

namespace GalensUnified.FileManagement;

public class BasicTextFileManager : ITextFileReader
{
    public List<string> GetTexts(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null) =>
        GetTexts(new DirectoryInfo(path), recursive, searchPattern, encoding);

    public List<string> GetTexts(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null)
    {
        if (!path.Exists)
            throw new DirectoryNotFoundException($"Directory not found: {path.FullName}");

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return [.. path.GetFiles(searchPattern, option).Select(f => File.ReadAllText(f.FullName, encoding ?? Encoding.UTF8))];
    }

    public string GetText(string path, string searchPattern = "*", Encoding? encoding = null) =>
        GetText(ResolveFilePath(path), searchPattern, encoding);

    public string GetText(FileInfo file, string searchPattern = "*", Encoding? encoding = null)
    {
        if (!file.Exists)
            throw new FileNotFoundException($"File not found: {file.FullName}");

        return File.ReadAllText(file.FullName);
    }

    public async Task<string> GetTextAsync(string path, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        await GetTextAsync(ResolveFilePath(path), searchPattern, encoding, cancellationToken);

    public async Task<string> GetTextAsync(FileInfo file, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (!file.Exists)
            throw new FileNotFoundException($"File not found: {file.FullName}");

        return await File.ReadAllTextAsync(file.FullName, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetTextsAsync(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var text in GetTextsAsync(new DirectoryInfo(path), recursive, searchPattern, encoding, cancellationToken))
            yield return text;
    }

    public async IAsyncEnumerable<string> GetTextsAsync(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!path.Exists)
            throw new DirectoryNotFoundException($"Directory not found: {path.FullName}");

        SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (string file in Directory.EnumerateFiles(path.FullName, searchPattern, option))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await File.ReadAllTextAsync(file, encoding ?? Encoding.UTF8, cancellationToken);
        }
    }

    protected virtual FileInfo ResolveFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found at: \"{path}\"");

        return new FileInfo(path);
    }
}
