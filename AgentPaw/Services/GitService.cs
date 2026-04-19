using System.IO;
using LibGit2Sharp;

namespace AgentPaw.Services;

public class GitService
{
    public void InitRepo(string repoPath)
    {
        Directory.CreateDirectory(repoPath);
        if (!Repository.IsValid(repoPath))
            Repository.Init(repoPath);
    }

    public string CommitAll(string repoPath, string message)
    {
        using var repo = new Repository(repoPath);
        Commands.Stage(repo, "*");
        var sig = new Signature("AgentPaw", "bot@agentpaw.local", DateTimeOffset.Now);
        var commit = repo.Commit(message, sig, sig);
        return commit.Sha;
    }

    public string GetCurrentCommitHash(string repoPath)
    {
        if (!Repository.IsValid(repoPath)) return string.Empty;
        using var repo = new Repository(repoPath);
        return repo.Head.Tip?.Sha ?? string.Empty;
    }

    public void ResetHard(string repoPath, string commitHash)
    {
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitHash);
        repo.Reset(ResetMode.Hard, commit);
    }
}
