using System.Text;
using GalensUnified.FileManagement;

namespace GalensUnified.CubicGrid.Framework;

public static class AssetManager
{
    private static DirectoryInfo? _assetsDirectory;
    public static DirectoryInfo AssetsDirectory
    {
        get
        {
            if (_assetsDirectory == null)
                throw new InvalidOperationException("Asset directory is not loaded.");
            return _assetsDirectory;
        }
    }
    private static ITextFileReader? _textFileReader;
    public static ITextFileReader TextFileReader
    {
        get
        {
            if (_textFileReader == null)
                throw new InvalidOperationException("A TextFileReader is not loaded.");
            return _textFileReader;
        }
        set => _textFileReader = value;
    }

    /// <summary>Sets a new AssetsDirectory and replaces all file managers with Defaults using the new path.</summary>
    public static void AssignAssetDirectory(DirectoryInfo assetsPath)
    {
        if (!assetsPath.Exists)
            throw new DirectoryNotFoundException($"Asset directory not found: {assetsPath.FullName}");
        _assetsDirectory = assetsPath;
        _textFileReader = new DefaultTextFileAssetManager(assetsPath);
    }

    public static List<string> GetTexts(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null) =>
        TextFileReader.GetTexts(path, recursive, searchPattern, encoding);

    public static List<string> GetTexts(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null) =>
        TextFileReader.GetTexts(path, recursive, searchPattern, encoding);

    public static string GetText(string path, string searchPattern = "*", Encoding? encoding = null) =>
        TextFileReader.GetText(path, searchPattern, encoding);

    public static string GetText(FileInfo file, string searchPattern = "*", Encoding? encoding = null) =>
        TextFileReader.GetText(file, searchPattern, encoding);

    public static IAsyncEnumerable<string> GetTextsAsync(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        TextFileReader.GetTextsAsync(path, recursive, searchPattern, encoding, cancellationToken);

    public static IAsyncEnumerable<string> GetTextsAsync(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        TextFileReader.GetTextsAsync(path, recursive, searchPattern, encoding, cancellationToken);

    public static Task<string> GetTextAsync(string path, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        TextFileReader.GetTextAsync(path, searchPattern, encoding, cancellationToken);

    public static Task<string> GetTextAsync(FileInfo file, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        TextFileReader.GetTextAsync(file, searchPattern, encoding, cancellationToken);
}
