
using LibGit2Sharp;

namespace SeBuild;

/// <summary>
/// Object managing downloaded packages stored in a local folder.
/// Tracks the currently downloaded repositories, their versions, and
/// </summary>
public sealed class PackageCache {
    /// Name of the directory that C# library sources are extracted to 
    public static readonly string CacheFolder = ".sebuild";
    /// Default host for git repositories
    public static readonly string DefaultHost = "github.com";
    
    /// Directory containing all .cs files or the root .csproj file
    private string _projectDir;

    /// Path to the directory used to cache downloaded libraries
    public string CachePath {
        get => Path.Combine(_projectDir, CacheFolder);
    }

    private struct RepositoryState {
        public Commit LatestCommit;
        public Repository Repository;
    }
    
    /// <summary>
    /// A map of package names to their git repository states
    /// </summary>
    private Dictionary<string, RepositoryState> _repos;
    
    /// <summary>
    /// Create a new package cache in the given project directory.
    /// The <paramref name="rootDir">directory</paramref> is the location of the root .csproj file or
    /// the location of all .cs files in the project.
    /// </summary>
    public PackageCache(string rootDir) {
        _projectDir = rootDir;
        _repos = new Dictionary<string, RepositoryState>();
        if(!Path.Exists(CachePath)) {
            Directory.CreateDirectory(CachePath);
        }

        foreach(var directory in Directory.GetDirectories(CachePath)) {
            var repo = new Repository(directory);
            var commit = repo.Head.Tip;
            var state = new RepositoryState() {
                LatestCommit = commit,
                Repository = repo,
            };

            _repos.Add(Path.GetFileName(directory)!, state);
        }
    }
    
    /// <summary>
    /// Download a package from the provided <paramref name="repository">repository name</paramref> or use a cached version if it is available.
    /// </summary>
    /// <returns>Path to the root of the loaded package</returns>
    public string GetPackage(string repository, string? folder, string? commit, string? host = null) {
        string folderName = RepositoryFolder(repository);
        var path = Path.Combine(CachePath, folderName);

        if(_repos.TryGetValue(folderName, out RepositoryState cachedRepository)) {
            if(commit is not null) {
                var requestedCommit = GetCommitForPrefix(cachedRepository.Repository, commit);
                if(requestedCommit.Author.When > cachedRepository.LatestCommit.Author.When) {
                    Console.WriteLine($"Using newer commit {requestedCommit.Id} over {cachedRepository.LatestCommit.Id} for repository {repository}");
                    Commands.Checkout(cachedRepository.Repository, requestedCommit);
                }
            }
        } else {
            Console.WriteLine($"Nothing found for {folderName}");
            host = host ?? DefaultHost;
            var uri = new UriBuilder() {
                Host = host,
                Path = repository
            };
            
            Console.WriteLine($"Downloading from repository {uri} - {repository}");

            Repository.Clone(
                uri.ToString(),
                path
            );

            var repo = new Repository(path);
            Commit selectedCommit = repo.Head.Tip;
            if(commit is not null) {
                selectedCommit = GetCommitForPrefix(repo, commit);
                Commands.Checkout(repo, selectedCommit);
            }

            _repos.Add(
                folderName,
                new RepositoryState() {
                    LatestCommit = selectedCommit,
                    Repository = repo,
                }
            );
        }


        return folder is null ? path : Path.Combine(path);
    }
    
    /// <summary>
    /// Get a commit object from the given repository matching the hash prefix
    /// </summary>
    private Commit GetCommitForPrefix(Repository repo, string pfx) {
        return
            repo.Commits.Single(c => c.Id.StartsWith(pfx)) ??
            throw new Exception($"Failed to fine commit matching the provided prefix {pfx} in repository {repo.Info.Path}");
    }
    
    /// <summary>
    /// Sanitize a github-style repository name to function as a folder name
    /// </summary>
    public string RepositoryFolder(string repoName) {
        return repoName.Replace('/', '.');
    }
}
