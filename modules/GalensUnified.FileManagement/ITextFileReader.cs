using System.Text;

namespace GalensUnified.FileManagement;

/// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="ITextFileReader"]/*' />
public interface ITextFileReader
{
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTexts(string path, bool recursive = false, Encoding? encoding = null)"]/*' />
    List<string> GetTexts(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTexts(DirectoryInfo path, bool recursive = false, Encoding? encoding = null)"]/*' />
    List<string> GetTexts(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetText(string path, Encoding? encoding = null)"]/*' />
    string GetText(string path, string searchPattern = "*", Encoding? encoding = null);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetText(FileInfo file, Encoding? encoding = null)"]/*' />
    string GetText(FileInfo file, string searchPattern = "*", Encoding? encoding = null);

    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTextsAsync(string path, bool recursive = false, Encoding? encoding = null, CancellationToken cancellationToken = default)"]/*' />
    IAsyncEnumerable<string> GetTextsAsync(string path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTextsAsync(DirectoryInfo path, bool recursive = false, Encoding? encoding = null, CancellationToken cancellationToken = default)"]/*' />
    IAsyncEnumerable<string> GetTextsAsync(DirectoryInfo path, bool recursive = false, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTextAsync(string path, Encoding? encoding = null, CancellationToken cancellationToken = default)"]/*' />
    Task<string> GetTextAsync(string path, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default);
    /// <include file='ITextFileReader.xml' path='MyDocs/MyMembers[@name="GetTextAsync(FileInfo file, Encoding? encoding = null, CancellationToken cancellationToken = default)"]/*' />
    Task<string> GetTextAsync(FileInfo file, string searchPattern = "*", Encoding? encoding = null, CancellationToken cancellationToken = default);
}