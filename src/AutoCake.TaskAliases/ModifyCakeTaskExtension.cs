using System;
using Cake.Core;

namespace AutoCake.TaskAliases
{
    public static class ModifyCakeTaskExtension
    {
        public static CakeTaskBuilder<ActionTask> DoesBefore(CakeTaskBuilder<ActionTask> task, Action<ICakeContext> t)
        {
            if (task == null)
            {
                throw new ArgumentNullException("task");
            }

            task.Task.Actions.Insert(0, t);
            return task;
        }

        public static CakeTaskBuilder<ActionTask> DoesBefore(CakeTaskBuilder<ActionTask> task, Action t)
        {
            return ModifyCakeTaskExtension.DoesBefore(task, c => t());
        }

        public static CakeTaskBuilder<ActionTask> DoesAfter(CakeTaskBuilder<ActionTask> task, Action<ICakeContext> t)
        {
            if (task == null)
            {
                throw new ArgumentNullException("task");
            }

            task.Task.Actions.Add(t);
            return task;
        }

        public static CakeTaskBuilder<ActionTask> DoesAfter(CakeTaskBuilder<ActionTask> task, Action t)
        {
            return ModifyCakeTaskExtension.DoesAfter(task, c => t());
        }
    }
}