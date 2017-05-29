using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core;

public static class MavenActions
{
    public static ICakeContext Context { get; private set; }
    public static MavenSettings Settings { get; set; }
    public static string GroupId { get; set; }
    public static string ArtifactId { get; set; }

    public static void Configure(ICakeContext c)
    {
        Context = c;
        Settings = new MavenSettings();

        if (Context.HasEnvironmentVariable("MAVEN_ALT_DEPLOY_REPOSITORY"))
        {
            Settings.Properties.Add("altDeploymentRepository", Context.EnvironmentVariable("MAVEN_ALT_DEPLOY_REPOSITORY"));
        }
        if (Context.HasEnvironmentVariable("MAVEN_SETTINGS_LOCATION"))
        {
            Settings.Properties.Add("altDeploymentRepository", Context.EnvironmentVariable("MAVEN_ALT_DEPLOY_REPOSITORY"));
        }
    }

    public static void RunMaven(string goal, MavenSettings settings = null)
    {
        ICakeContext context = Context;
        MavenRunner runner = new MavenRunner(context.FileSystem,
                                             context.Environment,
                                             context.Globber,
                                             context.ProcessRunner,
                                             context.Tools,
                                             context.Log);

        MavenSettings s = new MavenSettings();
        s.Merge(Settings);
        s.Merge(settings);
        s.Goal.Add(goal);
        runner.ExecuteScript(s);
    }

    public static void RunMaven(MavenSettings settings = null)
    {
        ICakeContext context = Context;
        MavenRunner runner = new MavenRunner(context.FileSystem,
                                             context.Environment,
                                             context.Globber,
                                             context.ProcessRunner,
                                             context.Tools,
                                             context.Log);

        MavenSettings s = new MavenSettings(settings);
        s.Merge(Settings);
        s.Merge(settings);
        runner.ExecuteScript(s);
    }

    public static void UpdateMavenVersionNumber(string typeOfChange, string version, MavenSettings settings)
    {
        ICakeContext context = Context;
        MavenRunner runner = new MavenRunner(context.FileSystem,
                                             context.Environment,
                                             context.Globber,
                                             context.ProcessRunner,
                                             context.Tools,
                                             context.Log);
        context.Information(string.Format("[{0}] Updating project to version {1} using {2}:{3}", typeOfChange, version, GroupId, ArtifactId));

        MavenSettings s = new MavenSettings(settings);
        s.Goal.Add("versions:set");
        s.Properties.Add("newVersion", version);
        if (!string.IsNullOrEmpty(GroupId))
        {
            s.Properties.Add("groupdId", GroupId);
        }
        if (!string.IsNullOrEmpty(ArtifactId))
        {
            s.Properties.Add("artifactId", ArtifactId);
        }
        runner.ExecuteScript(s);
    }

    public static void UpdateMavenVersionNumber(string typeOfChange, string version)
    {
        UpdateMavenVersionNumber(typeOfChange, version, Settings);
    }
}