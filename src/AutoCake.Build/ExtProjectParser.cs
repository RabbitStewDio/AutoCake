// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Cake.Common.Solution.Project;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

/// <summary>
///     The MSBuild project file parser.
/// </summary>
public sealed class ExtProjectParser
{
    readonly ICakeContext _context;
    readonly ICakeEnvironment _environment;

    readonly IFileSystem _fileSystem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectParser" /> class.
    /// </summary>
    public ExtProjectParser(ICakeContext context)
    {
        if (context == null)
            throw new ArgumentNullException("context");
        _context = context;
        _fileSystem = context.FileSystem;
        _environment = context.Environment;
    }

    bool MatchesCondition(XElement propertyGroup, string configuration, string platform)
    {
        var value = (string) propertyGroup.Attribute("Condition");
        if (string.IsNullOrEmpty(value))
            return true;
        if (value.Contains(string.Concat("'$(Configuration)|$(Platform)' == '", configuration, "|", platform, "'")))
            return true;
        if (value.Contains(string.Concat("'$(Configuration)' == '", configuration, "'")))
            return true;
        if (value.Contains(string.Concat("'$(Platform)' == '", platform, "'")))
            return true;
        return false;
    }

    /// <summary>
    ///     Parses a project file.
    /// </summary>
    /// <param name="parsedProjectPath">The project path.</param>
    /// <param name="configuration"></param>
    /// <param name="platform"></param>
    /// <returns>The parsed project.</returns>
    public ExtProjectParserResult Parse(FilePath parsedProjectPath, FilePath originalProjectFile, string configuration,
        string platform)
    {
        if (parsedProjectPath == null)
            throw new ArgumentNullException("parsedProjectPath");

        if (parsedProjectPath.IsRelative)
            parsedProjectPath = parsedProjectPath.MakeAbsolute(_environment);

        // Get the project file.
        var file = _fileSystem.GetFile(parsedProjectPath);
        if (!file.Exists)
        {
            const string format = "Project file '{0}' does not exist.";
            var message = string.Format(CultureInfo.InvariantCulture, format, parsedProjectPath.FullPath);
            throw new CakeException(message);
        }

        _context.Log.Debug("Parsing " + BuildConfig.AsRelativePath(parsedProjectPath) +
                           " for $(Configuration)|$(Platform) == " + configuration + "|" + platform);

        XDocument document;
        using (var stream = file.OpenRead())
        {
            document = XDocument.Load(stream);
        }

        if (document.Root == null)
            throw new Exception(
                "Silly error: The parser should never make the root element of a document into a null value");

        var projectProperties = new Dictionary<string, string>();
        projectProperties.Add("Platform", platform);
        projectProperties.Add("Configuration", configuration);

        // Parsing the build file is sensitive to the declared order of elements.
        // If there are include files, then these files must be honored too.
        ParseProjectProperties(configuration, platform, document, projectProperties);

        var rootPath = originalProjectFile.GetDirectory();

        // We only list compile elements
        var projectFiles =
        (from project in document.Elements(ProjectXElement.Project)
            from itemGroup in project.Elements(ProjectXElement.ItemGroup)
            from element in itemGroup.Elements()
            where element.Name == ProjectXElement.Compile ||
                  element.Name == ProjectXElement.Content
            from include in element.Attributes("Include")
            let value = include.Value
            where !string.IsNullOrEmpty(value)
            let filePath = rootPath.CombineWithFilePath(value)
            select new ProjectFile
            {
                FilePath = filePath,
                RelativePath = value,
                Compile = element.Name == ProjectXElement.Compile
            }).ToArray();

        var references =
        (from project in document.Elements(ProjectXElement.Project)
            from itemGroup in project.Elements(ProjectXElement.ItemGroup)
            from element in itemGroup.Elements()
            where element.Name == ProjectXElement.Reference
            from include in element.Attributes("Include")
            let includeValue = include.Value
            let hintPathElement = element.Element(ProjectXElement.HintPath)
            let nameElement = element.Element(ProjectXElement.Name)
            let fusionNameElement = element.Element(ProjectXElement.FusionName)
            let specificVersionElement = element.Element(ProjectXElement.SpecificVersion)
            let aliasesElement = element.Element(ProjectXElement.Aliases)
            let privateElement = element.Element(ProjectXElement.Private)
            select new ProjectAssemblyReference
            {
                Include = includeValue,
                HintPath = string.IsNullOrEmpty((string) hintPathElement)
                    ? null
                    : rootPath.CombineWithFilePath(hintPathElement.Value),
                Name = (string) nameElement,
                FusionName = (string) fusionNameElement,
                SpecificVersion =
                    specificVersionElement == null ? (bool?) null : bool.Parse(specificVersionElement.Value),
                Aliases = (string) aliasesElement,
                Private = privateElement == null ? (bool?) null : bool.Parse(privateElement.Value)
            }).ToArray();

        var projectReferences =
        (from project in document.Elements(ProjectXElement.Project)
            from itemGroup in project.Elements(ProjectXElement.ItemGroup)
            from element in itemGroup.Elements()
            where element.Name == ProjectXElement.ProjectReference
            from include in element.Attributes("Include")
            let value = include.Value
            where !string.IsNullOrEmpty(value)
            let filePath = rootPath.CombineWithFilePath(value)
            let nameElement = element.Element(ProjectXElement.Name)
            let projectElement = element.Element(ProjectXElement.Project)
            let packageElement = element.Element(ProjectXElement.Package)
            select new ProjectReference
            {
                FilePath = filePath,
                RelativePath = value,
                Name = (string) nameElement,
                Project = (string) projectElement,
                Package =
                    string.IsNullOrEmpty((string) packageElement)
                        ? null
                        : rootPath.CombineWithFilePath(packageElement.Value)
            }).ToArray();

        var outputPath = GetValueOrDefault(projectProperties, "OutputPath");
        if (string.IsNullOrEmpty(outputPath))
            return null;

        return new ExtProjectParserResult(
            GetValueOrDefault(projectProperties, "Configuration"),
            GetValueOrDefault(projectProperties, "Platform"),
            // Some projects with default configurations do not explicitly specify the platform-target.
            GetValueOrDefault(projectProperties, "PlatformTarget", GetValueOrDefault(projectProperties, "Platform")),
            GetValueOrDefault(projectProperties, "ProjectGuid"),
            GetValueOrDefault(projectProperties, "OutputType", "Library"),
            GetValueOrDefault(projectProperties, "OutputPath"),
            GetValueOrDefault(projectProperties, "RootNameSpace"),
            GetValueOrDefault(projectProperties, "AssemblyName"),
            GetValueOrDefault(projectProperties, "TargetFrameworkVersion"),
            GetValueOrDefault(projectProperties, "TargetFrameworkProfile"),
            projectFiles,
            references,
            projectReferences);
    }

    string GetValueOrDefault(Dictionary<string, string> d, string key, string value = null)
    {
        string result;
        if (d.TryGetValue(key, out result))
        {
          return ResolveProperty(result, d);
        }
        return value;
    }

    string ResolveProperty(string property, Dictionary<string, string> pool)
    {
        // crude replace 
        foreach (var pair in pool)
        {
            property = property.Replace("$(" + pair.Key + ")", pair.Value);
        }
        return property;
    }

    void ParseProjectProperties(string configuration, string platform, XDocument document,
        Dictionary<string, string> rawProperties)
    {
        foreach (var element in document.Root.Elements())
            if (element.Name == ProjectXElement.PropertyGroup)
            {
                if (!MatchesCondition(element, configuration, platform))
                    continue;

                foreach (var prop in element.Elements())
                {
                    var cond = (string) prop.Attribute("Condition");
                    if (!string.IsNullOrEmpty(cond))
                        continue;

                    var name = prop.Name.LocalName;
                    var value = (string) prop;
                    rawProperties[name] = value;
                }
            }
            else if (element.Name == ProjectXElement.Import)
            {
                var fileRef = (string) element.Attribute(ProjectXElement.Project);
                if (fileRef == null)
                    continue;

                var filePath = new FilePath(fileRef);
                if (_fileSystem.Exist(filePath))
                {
                    XDocument includedDocument;
                    var file = _fileSystem.GetFile(filePath);
                    using (var stream = file.OpenRead())
                    {
                        includedDocument = XDocument.Load(stream);
                    }
                    ParseProjectProperties(configuration, platform, includedDocument, rawProperties);
                }
            }
    }

    /// <summary>MSBuild Project Xml Element XNames</summary>
    internal static class ProjectXElement
    {
        const string XmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        /// <summary>Project root element</summary>
        internal const string Project = "{http://schemas.microsoft.com/developer/msbuild/2003}Project";

        /// <summary>Item group element</summary>
        internal const string ItemGroup = "{http://schemas.microsoft.com/developer/msbuild/2003}ItemGroup";

        /// <summary>Assembly reference element</summary>
        internal const string Reference = "{http://schemas.microsoft.com/developer/msbuild/2003}Reference";

        /// <summary>Namespace import element</summary>
        internal const string Import = "{http://schemas.microsoft.com/developer/msbuild/2003}Import";

        /// <summary>Namespace compile element</summary>
        internal const string Compile = "{http://schemas.microsoft.com/developer/msbuild/2003}Compile";

        /// <summary>Namespace content element</summary>
        internal const string Content = "{http://schemas.microsoft.com/developer/msbuild/2003}Content";

        /// <summary>Namespace property group element</summary>
        internal const string PropertyGroup = "{http://schemas.microsoft.com/developer/msbuild/2003}PropertyGroup";

        /// <summary>Namespace root namespace element</summary>
        internal const string RootNamespace = "{http://schemas.microsoft.com/developer/msbuild/2003}RootNamespace";

        /// <summary>Namespace output type element</summary>
        internal const string OutputType = "{http://schemas.microsoft.com/developer/msbuild/2003}OutputType";

        /// <summary>Namespace output path element</summary>
        internal const string OutputPath = "{http://schemas.microsoft.com/developer/msbuild/2003}OutputPath";

        /// <summary>Namespace assembly name element</summary>
        internal const string AssemblyName = "{http://schemas.microsoft.com/developer/msbuild/2003}AssemblyName";

        /// <summary>
        ///     Gets the namespace for the target framework version element.
        /// </summary>
        internal const string TargetFrameworkVersion =
            "{http://schemas.microsoft.com/developer/msbuild/2003}TargetFrameworkVersion";

        /// <summary>
        ///     Gets the namespace for the target framework version element.
        /// </summary>
        internal const string TargetFrameworkProfile =
            "{http://schemas.microsoft.com/developer/msbuild/2003}TargetFrameworkProfile";

        /// <summary>Gets the namespace for the configuration element.</summary>
        internal const string Configuration = "{http://schemas.microsoft.com/developer/msbuild/2003}Configuration";

        /// <summary>Gets the namespace for the platform element.</summary>
        internal const string Platform = "{http://schemas.microsoft.com/developer/msbuild/2003}Platform";

        /// <summary>Gets the namespace for the project GUID.</summary>
        internal const string ProjectGuid = "{http://schemas.microsoft.com/developer/msbuild/2003}ProjectGuid";

        /// <summary>
        ///     Gets the namespace for the bootstrapper package element.
        /// </summary>
        internal const string BootstrapperPackage =
            "{http://schemas.microsoft.com/developer/msbuild/2003}BootstrapperPackage";

        /// <summary>Gets the namespace for the project reference element.</summary>
        internal const string ProjectReference = "{http://schemas.microsoft.com/developer/msbuild/2003}ProjectReference";

        /// <summary>Gets the namespace for the service element.</summary>
        internal const string Service = "{http://schemas.microsoft.com/developer/msbuild/2003}Service";

        /// <summary>Gets the namespace for the hint path element.</summary>
        internal const string HintPath = "{http://schemas.microsoft.com/developer/msbuild/2003}HintPath";

        /// <summary>Gets the namespace for the name element.</summary>
        internal const string Name = "{http://schemas.microsoft.com/developer/msbuild/2003}Name";

        /// <summary>Gets the namespace for the fusion name element.</summary>
        internal const string FusionName = "{http://schemas.microsoft.com/developer/msbuild/2003}FusionName";

        /// <summary>Gets the namespace for the specific version element.</summary>
        internal const string SpecificVersion = "{http://schemas.microsoft.com/developer/msbuild/2003}SpecificVersion";

        /// <summary>Gets the namespace for the aliases element.</summary>
        internal const string Aliases = "{http://schemas.microsoft.com/developer/msbuild/2003}Aliases";

        /// <summary>Gets the namespace for the private element.</summary>
        internal const string Private = "{http://schemas.microsoft.com/developer/msbuild/2003}Private";

        /// <summary>Gets the namespace for the package element.</summary>
        internal const string Package = "{http://schemas.microsoft.com/developer/msbuild/2003}Package";
    }
}