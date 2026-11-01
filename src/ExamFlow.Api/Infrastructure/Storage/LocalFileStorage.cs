namespace ExamFlow.Api.Infrastructure.Storage;

/// <summary>Abstraction so uploads can later move to blob storage.</summary>
public interface IFileStorage
{
    Task SaveAsync(string fileName, Stream content, CancellationToken ct);
    Stream OpenRead(string fileName);
    void Delete(string fileName);
}

/// <summary>Stores files in a folder outside wwwroot. File names are always server-generated.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Storage:RootPath"] ?? Path.Combine("App_Data", "uploads");
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string fileName, Stream content, CancellationToken ct)
    {
        await using var file = new FileStream(PathFor(fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public Stream OpenRead(string fileName) =>
        new FileStream(PathFor(fileName), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public void Delete(string fileName)
    {
        var path = PathFor(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage file name.");
        }

        return path;
    }
}
