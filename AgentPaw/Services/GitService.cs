using System.IO;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace AgentPaw.Services;

public class GitService
{
    /// <summary>
    /// 원격 레포를 targetPath에 클론한다.
    /// token이 있으면 HTTPS Bearer 인증에 사용한다 (GitHub PAT, GitLab token 등).
    /// </summary>
    public Task<string> CloneAsync(
        string url,
        string targetPath,
        string? token = null,
        IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(targetPath) && Repository.IsValid(targetPath))
                throw new InvalidOperationException($"이미 Git 레포가 존재한다: {targetPath}");

            Directory.CreateDirectory(targetPath);

            CredentialsHandler? credHandler = null;
            if (!string.IsNullOrWhiteSpace(token))
            {
                credHandler = (_, _, _) => new UsernamePasswordCredentials
                {
                    Username = token,
                    Password = string.Empty
                };
            }

            var options = new CloneOptions();
            options.FetchOptions.OnProgress        = msg => { progress?.Report(msg?.TrimEnd() ?? string.Empty); return true; };
            options.FetchOptions.OnTransferProgress = p  => { progress?.Report($"객체 수신: {p.ReceivedObjects}/{p.TotalObjects}"); return true; };
            if (credHandler != null)
                options.FetchOptions.CredentialsProvider = credHandler;

            var clonedPath = Repository.Clone(url, targetPath, options);
            return clonedPath;
        });
    }

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
