# AutoCake Maven Module

This module enables Cake to call Maven and to build Java projects.
To build a Maven project, use the following Cake snippet:

    #reference "tools/AutoCake.Maven/tools/AutoCake.Maven.dll"
    #load      "tools/AutoCake.Maven/tools/tasks.cake"

    CreateDirectory("build-artefacts/maven");
    MavenActions.Settings.Properties.Add("altDeploymentRepository", "target::default::file:./build-artefacts/maven");

    Task("Default")
       .Does(() => {
         // Simple mode: MavenActions.RunMaven("deploy");

         MavenActions.RunMaven("compile", new MavenSettings(MavenActions.Settings) {
           Goal = {
             "clean",
             "package"
           }
         );

       });

The default configuation checks for two environment variables that allow you
to define the deployment repository and the settings file.

If ``MAVEN_ALT_DEPLOY_REPOSITORY`` is defined, this will set the Maven system property 
``altDeploymentRepository``. Maven requires this property to deploy built artefacts.

You can specify a global maven settings file via ``MAVEN_SETTINGS_LOCATION``. This
will specify the referenced file as "-s" option on the command line.