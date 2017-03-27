using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Core;

public static class TaskActions
{
    public static IReadOnlyList<CakeTask> Tasks { get; private set; }
    public static ICakeContext Context { get; private set; }

    public static void Configure(ICakeContext context,
        IReadOnlyList<CakeTask> tasks)
    {
        Tasks = tasks;
        Context = context;
    }

    public static CakeTaskBuilder<ActionTask> ModifyTask(string name)
    {
        var task = Tasks.First(t => t.Name == name) as ActionTask;
        if (task == null)
            throw new ArgumentException(string.Format("There is no preexisting task with name {0}", name));
        return new CakeTaskBuilder<ActionTask>(task);
    }
}