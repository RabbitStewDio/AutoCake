using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Core;
using Cake.Core.Annotations;

namespace AutoCake.TaskAliases
{
    [CakeAliasCategory("AutoCake")]
    [CakeNamespaceImport("AutoCake.TaskAliases")]
    public static class AutoCakeAliases
    {
        public static IReadOnlyList<CakeTask> Tasks { get; private set; }

        public static void Configure(IReadOnlyList<CakeTask> tasks)
        {
            Tasks = tasks;
        }

        [CakeMethodAlias]
        [CakeNamespaceImport("Cake.Common.IO.Paths")]
        public static CakeTaskBuilder<ActionTask> ModifyTask(this ICakeContext context, string name)
        {
            if (Tasks == null)
            {
                // ugh, yeah, it is crude. But cake does not expose the current script in any other way.
                throw new Exception("Please add 'AutoCake.TaskAliases.Configure(Tasks);' to your script before calling this alias.");
            }

            var task = Tasks.First(t => t.Name == name) as ActionTask;
            if (task == null)
            {
                throw new ArgumentException(string.Format("There is no preexisting task with name {0}", name));
            }
            return new CakeTaskBuilder<ActionTask>(task);
        }
    }
}
