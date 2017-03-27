# AutoCake Task Aliases

This little add-on contains an easy helper that allows you to modify existing 
tasks by looking up an existing task by name in the list of registered tasks.

The extension also adds some convenience methods for running additional code before
and after a given task.

Cake does not volunteer the task list to extensions, so you have to call a setup
method first before you can use these helpers:

    // The Tasks property exists on all script instances as part of Cake's scripting setup.
    AutoCake.TaskAliases.AutoCakeAliases.Configure(Tasks);
    
The simplest way of getting the functionality is to add the following line to your 
build script.

    #load "tools/AutoCake.TaskAliases/tools/task-aliases.cake"
    
After that you can lookup existing tasks via

    ModifyTask("myTask").DoesBefore(() => { 
       Information("Hello World");
    });

ModifyTask returns a CakeTaskBuilder\<ActionTask\>, so anything you can do with a Task("name")
you can do with the modified task too.
