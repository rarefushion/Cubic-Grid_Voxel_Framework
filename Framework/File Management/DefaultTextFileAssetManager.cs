using GalensUnified.FileManagement;

namespace GalensUnified.CubicGrid.Framework;

public class DefaultTextFileAssetManager : BasicTextFileManager
{
    readonly DirectoryInfo assetDirectory;

    protected override FileInfo ResolveFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        string finalPath = string.Empty;
        string subPath = Path.Combine(assetDirectory.FullName, path);
        if (File.Exists(subPath))
            finalPath = subPath;
        else if (File.Exists(path))
            finalPath = path;

        if (string.IsNullOrWhiteSpace(finalPath))
            throw new FileNotFoundException($"Asset not found at relative path: \"{subPath}\" or absolute path: \"{path}\"");

        return new FileInfo(finalPath);
    }

    protected override DirectoryInfo ResolveDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        string finalPath = string.Empty;
        string subPath = Path.Combine(assetDirectory.FullName, path);
        if (Path.Exists(subPath))
            finalPath = subPath;
        else if (Path.Exists(path))
            finalPath = path;

        if (string.IsNullOrWhiteSpace(finalPath))
            throw new DirectoryNotFoundException($"Directory folder not found at relative path: \"{subPath}\" or absolute path: \"{path}\"");

        return new DirectoryInfo(finalPath);
    }

    public DefaultTextFileAssetManager(string assetDirectory)
    {
        this.assetDirectory = new DirectoryInfo(assetDirectory);
        if (!this.assetDirectory.Exists)
            throw new DirectoryNotFoundException($"Asset directory not found: {this.assetDirectory.FullName}");
    }

    public DefaultTextFileAssetManager(DirectoryInfo assetDirectory)
    {
        this.assetDirectory = assetDirectory;
        if (!this.assetDirectory.Exists)
            throw new DirectoryNotFoundException($"Asset directory not found: {this.assetDirectory.FullName}");
    }
}