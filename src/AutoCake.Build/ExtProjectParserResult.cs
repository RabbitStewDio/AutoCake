using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Solution.Project;
using Cake.Core.IO;

/// <summary>Represents the content in an MSBuild project file.</summary>
public class ExtProjectParserResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="T:Cake.Common.Solution.Project.ProjectParserResult" /> class.
    /// </summary>
    /// <param name="configuration">The build configuration.</param>
    /// <param name="platform">The target platform.</param>
    /// <param name="platformTarget"></param>
    /// <param name="projectGuid">The unique project identifier.</param>
    /// <param name="outputType">The compiler output type.</param>
    /// <param name="outputPath">The compiler output path</param>
    /// <param name="rootNameSpace">The default root namespace.</param>
    /// <param name="assemblyName">Gets the build target assembly name.</param>
    /// <param name="targetFrameworkVersion">The compiler framework version.</param>
    /// <param name="targetFrameworkProfile">The compiler framework profile.</param>
    /// <param name="files">The project content files.</param>
    /// <param name="references">The references.</param>
    /// <param name="projectReferences">The references to other projects.</param>
    public ExtProjectParserResult(string configuration,
        string platform,
        string platformTarget,
        string projectGuid,
        string outputType,
        string outputPath,
        string rootNameSpace,
        string assemblyName,
        string targetFrameworkVersion,
        string targetFrameworkProfile,
        IEnumerable<ProjectFile> files,
        IEnumerable<ProjectAssemblyReference> references,
        IEnumerable<ProjectReference> projectReferences)
    {
        if (String.IsNullOrEmpty(platform))
        {
            throw new ArgumentException("platform");
        }

        if (String.IsNullOrEmpty(configuration))
        {
            throw new ArgumentException("configuration");
        }

        Configuration = configuration;
        Platform = platform;
        PlatformTarget = platformTarget;
        ProjectGuid = projectGuid;
        OutputType = outputType;
        OutputPath = outputPath;
        RootNameSpace = rootNameSpace;
        AssemblyName = assemblyName;
        TargetFrameworkVersion = targetFrameworkVersion;
        TargetFrameworkProfile = targetFrameworkProfile;
        Files = files.ToList().AsReadOnly();
        References = references.ToList().AsReadOnly();
        ProjectReferences = projectReferences.ToList().AsReadOnly();

        if (outputPath.Contains("$("))
        {
            throw new Exception("The output path has not been resolved:" + outputPath);
        }
    }

    /// <summary>Gets the build configuration.</summary>
    /// <value>The build configuration.</value>
    public string Configuration { get; private set; }

    /// <summary>Gets the target platform.</summary>
    /// <value>The platform.</value>
    public string Platform { get; private set; }

    /// <summary>Gets the target platform.</summary>
    /// <value>The platform.</value>
    public string PlatformTarget { get; private set; }

    /// <summary>Gets the unique project identifier.</summary>
    /// <value>The unique project identifier.</value>
    public string ProjectGuid { get; private set; }

    /// <summary>
    ///     Gets the compiler output type, i.e. <c>Exe/Library</c>.
    /// </summary>
    /// <value>The output type.</value>
    public string OutputType { get; private set; }

    /// <summary>Gets the compiler output path.</summary>
    /// <value>The output path.</value>
    public DirectoryPath OutputPath { get; private set; }

    /// <summary>Gets the default root namespace.</summary>
    /// <value>The root namespace.</value>
    public string RootNameSpace { get; private set; }

    /// <summary>Gets the build target assembly name.</summary>
    /// <value>The assembly name.</value>
    public string AssemblyName { get; private set; }

    /// <summary>Gets the compiler target framework version.</summary>
    /// <value>The target framework version.</value>
    public string TargetFrameworkVersion { get; private set; }

    /// <summary>Gets the compiler target framework profile.</summary>
    /// <value>The target framework profile.</value>
    public string TargetFrameworkProfile { get; private set; }

    /// <summary>Gets the project content files.</summary>
    /// <value>The files.</value>
    public ICollection<ProjectFile> Files { get; private set; }

    /// <summary>Gets the references.</summary>
    /// <value>The references.</value>
    public ICollection<ProjectAssemblyReference> References { get; private set; }

    /// <summary>Gets the references to other projects.</summary>
    /// <value>The references.</value>
    public ICollection<ProjectReference> ProjectReferences { get; private set; }
}