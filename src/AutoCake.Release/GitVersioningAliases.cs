using Cake.Common.Tools.GitVersion;
using Cake.Core;

public static class GitVersioningAliases
{
    public static ICakeContext Context { get; private set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
    }

    public static GitVersion FetchVersion()
    {
        var assertedVersions = Context.GitVersion(new GitVersionSettings {OutputType = GitVersionOutput.Json});

        return assertedVersions;
    }
}