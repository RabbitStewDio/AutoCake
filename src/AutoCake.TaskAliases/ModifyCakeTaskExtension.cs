using System;
using System.Threading.Tasks;
using Cake.Core;

namespace AutoCake.TaskAliases
{
    public static class ModifyCakeTaskExtension
    {
        public static CakeTaskBuilder DoesBefore(CakeTaskBuilder task, Func<ICakeContext, Task> t)
        {
            if (task == null)
            {
                throw new ArgumentNullException("task");
            }

            var ct = task.Task as CakeTask;
            ct.Actions.Insert(0, t);
            return task;
        }

        public static CakeTaskBuilder DoesBefore(CakeTaskBuilder task, Action t)
        {
            return ModifyCakeTaskExtension.DoesBefore(task, c =>
            {
                t();
                return Task.CompletedTask;
            });
        }

        public static CakeTaskBuilder DoesAfter(CakeTaskBuilder task, Func<ICakeContext, Task> t)
        {
            if (task == null)
            {
                throw new ArgumentNullException("task");
            }

            var ct = task.Task as CakeTask;
            ct.Actions.Add(t);
            return task;
        }

        public static CakeTaskBuilder DoesAfter(CakeTaskBuilder task, Action t)
        {
            return ModifyCakeTaskExtension.DoesAfter(task, c =>
            {
                t();
                return Task.CompletedTask;
            });
        }
    }
}