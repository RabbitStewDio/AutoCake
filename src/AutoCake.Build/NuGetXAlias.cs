using System;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.IO.NuGet;

public static class NuGetXAlias
{
    public static void NuGetPush(ICakeContext context, FilePath packageFilePath, NuGetXPushSettings settings)
    {
        if (context == null)
            throw new ArgumentNullException("context");

        var resolver = new NuGetToolResolver(context.FileSystem, context.Environment, context.Tools);
        var packer = new NuGetXPusher(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools,
            resolver, context.Log);
        packer.Push(packageFilePath, settings);
    }
}