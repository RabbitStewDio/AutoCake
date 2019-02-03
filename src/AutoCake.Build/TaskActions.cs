using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cake.Core;

public static class TaskActions
{
    public static IReadOnlyList<ICakeTaskInfo> Tasks { get; private set; }
    public static ICakeContext Context { get; private set; }

    public static void Configure(ICakeContext context,
                                 IReadOnlyList<ICakeTaskInfo> tasks)
    {
        Tasks = tasks;
        Context = context;
    }

    public static CakeTaskBuilder ModifyTask(string name)
    {
        var task = Tasks.First(t => t.Name == name);
        if (task == null)
            throw new ArgumentException(string.Format("There is no preexisting task with name {0}", name));
        return CreateModifiedTaskBuilder(task);
    }

    public static CakeTaskBuilder CreateModifiedTaskBuilder(ICakeTaskInfo task)
    {
        try
        {
            var builderType = typeof(CakeTaskBuilder);
            var constr = builderType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] {typeof(CakeTask)}, null);
            return (CakeTaskBuilder) constr.Invoke(null, new[] {task});
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to create task builder.", ex);
        }
    }
}