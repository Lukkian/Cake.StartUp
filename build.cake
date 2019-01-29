#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
//#addin nuget:?package=Cake.ClickTwice
#addin nuget:?package=Cake.Powershell&version=0.4.7
#r "C:\Users\lukia\source\repos\Cake\ClickTwice\src\Cake.ClickTwice\bin\Debug\Cake.ClickTwice.dll"
#r "C:\Users\lukia\source\repos\Cake\ClickTwice\src\Cake.ClickTwice\bin\Debug\ClickTwice.Handlers.AppDetailsPage.dll"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var version = Argument("version", "1.0.0");

var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solution = "Example";
var mainproject = "WindowsFormsApp";
var testtarget = "*.Tests";
var artifacts = "./artifacts/";
var mainprojectpath = $"./src/{mainproject}/{mainproject}.csproj";
var solutionpath = $"./src/{solution}.sln";
var publishpath = $"./{artifacts}/publish";

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var solutionObj = ParseSolution(solutionpath);
var projects = solutionObj.Projects;
var projectPaths = projects.Select(p => p.Path.GetDirectory());
//var testAssemblies = projects.Where(p => p.Name.Contains("Tests")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
//var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));

// Define directories.
var buildDir = Directory($"./src/{mainproject}/bin") + Directory(configuration);
var publishDir = Directory(publishpath);

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	Information($"Building for soulution {solutionpath}");
	Information($"Main project {mainprojectpath}");

    // Versioning
    var propsFile = "./src/ClickOnce.props";
    if (FileExists(propsFile) == false)
        throw new Exception("Build aborted: file ClickOnce.props not found!");
    Information($"ClickOnce props file: {propsFile}");

    var storedVersion = XmlPeek(propsFile, "//ApplicationVersion");
    var previousVersion = new Version(storedVersion);
    Information($"Previous version: {previousVersion}");

    var currentVersion = new Version(previousVersion.Major, previousVersion.Minor, previousVersion.Build + 1);

    var versionArg = new Version(version);
    if(versionArg > currentVersion)
        currentVersion = versionArg;
    version = currentVersion.ToString();

    Information($"Current version: {currentVersion}");

    XmlPoke(propsFile, "//ApplicationRevision", currentVersion.Revision.ToString());
    XmlPoke(propsFile, "//ApplicationVersion", currentVersion.ToString());

    var nextVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build + 1);
    Information($"Next version: {nextVersion}");

    // Change the version of ClickOnce in the project file, without this change the package will generate the wrong version.
    Information("Patching project file with new version number...");
    XmlPoke(mainprojectpath, "/ns:Project/ns:PropertyGroup/ns:ApplicationVersion", $"{version}.0",
        new XmlPokeSettings { Namespaces = new Dictionary<string, string> {
            { "ns", "http://schemas.microsoft.com/developer/msbuild/2003" }
        }
    });
    Information($"\r\rProject file version patched to: {version}.0");

    Information("Patching AssemblyInfo with new version number...");
    // Set the version in all the AssemblyInfo.cs or AssemblyInfo.vb files in any subdirectory
    StartPowershellFile("./SetAssemblyInfoVersion.ps1", args => { args.Append("Version", $"{version}.0"); });
    Information($"AssemblyInfo version patched to: {version}.0");
});

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");

    version = "1.0.0.0";
    Information($"Reseting version number to: {version}");

    // Reseting the version of ClickOnce in the project file
    XmlPoke(mainprojectpath, "/ns:Project/ns:PropertyGroup/ns:ApplicationVersion", version,
        new XmlPokeSettings { Namespaces = new Dictionary<string, string> {
            { "ns", "http://schemas.microsoft.com/developer/msbuild/2003" }
        }
    });
    Information("Project version reseted");

    // Restore the default version in all the AssemblyInfo.cs or AssemblyInfo.vb files in any subdirectory
    StartPowershellFile("./SetAssemblyInfoVersion.ps1", args => { args.Append("Version", version); });
    Information("AssemblyInfo version reseted");
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in projectPaths)
    {
        Information($"Cleaning {path}");
        CleanDirectories($"{path}/**/bin/");
        CleanDirectories($"{path}/**/obj/");
    }
    Information($"Cleaning: {publishDir}");
    CleanDirectory(publishDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    // Restore all NuGet packages.
    Information("Restore all NuGet packages...");
    NuGetRestore(solutionpath);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
	Information("Building solution...");
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solutionpath, settings =>
        settings.SetConfiguration(configuration)
        //.SetVerbosity(Verbosity.Diagnostic)
        .SetVerbosity(Verbosity.Quiet));
    }
    else
    {
      // Use XBuild
      XBuild(solutionpath, settings =>
        settings.SetConfiguration(configuration)
        //.SetVerbosity(Verbosity.Diagnostic)
        .SetVerbosity(Verbosity.Quiet));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information($"Running test for {testtarget}.dll");
    Information($"Mode: {configuration}");
    NUnit3($"./src/**/bin/{configuration}/{testtarget}.dll", new NUnit3Settings { NoResults = true });
});

Task("Publish-ClickOnce")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    Information("Publish ClickOnce...");
    Information($"Mode: {configuration}");
    PublishApp(mainprojectpath)
    	.SetConfiguration(configuration)
        //.ForceRebuild()
        //.SetBuildPlatform(MSBuildPlatform.x86)
        //.SetBuildPlatform(MSBuildPlatform.x64)
        //.SetBuildPlatform(MSBuildPlatform.AnyCPU)
        //.DoNotBuild()
        .ThrowOnHandlerFailure()
        .WithVersion(version)
    	.To(publishpath);
})
.OnError(ex =>
{
    // Handle the error here.
    Information("Message: " + ex.Message);
    Information("StackTrace: " + ex.StackTrace);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Publish-ClickOnce");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
