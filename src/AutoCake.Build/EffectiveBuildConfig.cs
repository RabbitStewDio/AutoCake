using System.Collections.Generic;
using System.Linq;
using Cake.Common.Tools.MSBuild;
using Cake.Core.IO;

public class EffectiveBuildConfig
{
    public string Configuration { get; set; }

    public List<FilePath> ProjectFiles { get; set; }

    public DirectoryPath SolutionDir { get; set; }

    public Dictionary<PlatformTarget, List<ParsedProject>> ProjectsByPlatform { get; set; }

    public List<PlatformTarget> PlatformBuildOrder { get; set; }

    public List<ParsedProject> Projects
    {
        get
        {
            var retval = ProjectsByPlatform.Values.FirstOrDefault();
            if (retval == null)
                return new List<ParsedProject>();
            return retval;
        }
    }

    public FilePath Solution { get; set; }
}