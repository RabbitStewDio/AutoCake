using System.Xml;
using Cake.Common.IO;
using Cake.Common.Xml;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public static class ReportActions
{
    public static ICakeContext Context { get; private set; }
    public static FilePath NUnitToHtmlTransformation { get; set; }

    public static void Configure(ICakeContext context)
    {
        Context = context;
        foreach (var file in Context.Globber.GetFiles("**/Transforms/HtmlTransform-v2.xslt"))
            if (Context.FileExists(file))
            {
                NUnitToHtmlTransformation = file;
                break;
            }
    }

    public static void GenerateHtmlTestReports()
    {
        if (NUnitToHtmlTransformation == null)
            return;

        foreach (var projectWithPlatform in BuildConfig.Config.ProjectsByPlatform)
        foreach (var project in projectWithPlatform.Value)
        {
            var output = BuildConfig.ComputeOutputPath("reports/html", project);
            var testPath = UnitTestActions.ComputeProjectUnitTestPath(project).FullPath;
            var pattern = testPath + "/*.nunit2.xml";

            foreach (var file in Context.Globber.GetFiles(pattern))
            {
                Context.CreateDirectory(output);

                var outputHtmlFile =
                    output.CombineWithFilePath(file.GetFilenameWithoutExtension() + ".html")
                        .MakeAbsolute(Context.Environment);
                Context.Log.Information("Creating report file " +
                                        Context.Environment.WorkingDirectory.GetRelativePath(outputHtmlFile));
                Context.XmlTransform(NUnitToHtmlTransformation, file, outputHtmlFile, new XmlTransformationSettings
                {
                    ConformanceLevel = ConformanceLevel.Fragment,
                    Indent = true,
                    NewLineHandling = NewLineHandling.None
                });
            }
        }
    }

    public static void GenerateSourceDocumentation()
    {
        // todo: Figure out how to make Wyam run here to generate a set of documentation files ready for publishing.
    }
}