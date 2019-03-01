#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0
//#addin nuget:?package=Cake.ClickTwice&version=0.2.0-unstable0003
#addin nuget:?package=Cake.Powershell&version=0.4.7

// Squirrel: It's like ClickOnce but Works
#tool nuget:?package=squirrel.windows&version=1.9.1
#addin nuget:?package=Cake.Squirrel&version=0.14.0

// To support ClickOnce and Cake 0.32.1, otherwise use: #addin nuget:?package=Cake.ClickTwice&version=0.2.0-unstable0003
#addin nuget:?package=Lukkian.Cake.ClickTwice&version=0.1.2

// To debug in dotnet core [preliminar support], otherwise use: #addin nuget:?package=Cake.Powershell&version=0.4.7
//#addin nuget:?package=Lukkian.Cake.Powershell&version=0.4.9

// To debug in VSCode uncomment below lines
// #r ".\tools\Addins\Lukkian.Cake.ClickTwice.0.1.2\lib\net45\Cake.ClickTwice.dll"
// #r ".\tools\Addins\Lukkian.Cake.ClickTwice.0.1.2\lib\net45\ClickTwice.Handlers.AppDetailsPage.dll"
// #r ".\tools\Addins\Lukkian.Cake.Powershell.0.4.9\lib\netcoreapp2.1\Cake.Core.Powershell.dll"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var version = Argument("version", "1.0.0");
var freezeversion = Argument("freezeversion", "true");

var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solution = "Example";
var mainproject = "WindowsFormsApp";
var testtarget = "*tests";
var artifacts = "./artifacts";
var mainprojectpath = $"./src/{mainproject}/{mainproject}.csproj";
var solutionpath = $"./src/{solution}.sln";
var publishpath = $"./{artifacts}/publish";
var nugetVersion = version;

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
	Information($"Solution: {solutionpath}");
	Information($"Main project: {mainprojectpath}");
    Information($"Mode: {configuration}");
    
    Information("Revision number will always be ignored and reset to zero.");

    // Versioning
    var propsFile = "./publish.props";
    if (FileExists(propsFile) == false)
        throw new Exception("Build aborted: file publish.props not found!");
    Information($"ClickOnce props file: {propsFile}");

    var storedVersion = XmlPeek(propsFile, "//ApplicationVersion");
    var previousVersion = new Version(storedVersion);
    Information($"Previous version: {previousVersion}");

    var currentVersion = new Version(previousVersion.Major, previousVersion.Minor, previousVersion.Build + 1, 0);

    var versionArg = new Version(version);
    
    if(versionArg > currentVersion)
        currentVersion = versionArg;

    if(freezeversion == "true")
    {
        Information($"Version number is in freeze mode: {previousVersion}");
        currentVersion = previousVersion;
    }

    version = currentVersion.ToString();
    nugetVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

    Information($"Current version: {currentVersion}");

    XmlPoke(propsFile, "//ApplicationRevision", "0");
    XmlPoke(propsFile, "//ApplicationVersion", currentVersion.ToString());

    var nextVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build + 1, 0);
    Information($"Next version: {nextVersion}");

    // Change the version of ClickOnce in the project file, without this change the package will generate the wrong version.
    Information("Patching project file with new version number...");
    XmlPoke(mainprojectpath, "/ns:Project/ns:PropertyGroup/ns:ApplicationVersion", version,
        new XmlPokeSettings { Namespaces = new Dictionary<string, string> {
            { "ns", "http://schemas.microsoft.com/developer/msbuild/2003" }
        }
    });
    XmlPoke(mainprojectpath, "/ns:Project/ns:PropertyGroup/ns:ApplicationRevision", "0",
        new XmlPokeSettings { Namespaces = new Dictionary<string, string> {
            { "ns", "http://schemas.microsoft.com/developer/msbuild/2003" }
        }
    });
    Information($"\r\rProject file version patched to: {version}");

    Information("Patching AssemblyInfo with new version number...");
    // Set the version in all the AssemblyInfo.cs or AssemblyInfo.vb files in any subdirectory
    StartPowershellFile("./SetAssemblyInfoVersion.ps1", args => { args.Append("Version", version); });
    Information($"AssemblyInfo version patched to: {version}");
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
        .SetVerbosity(Verbosity.Minimal));
    }
    else
    {
      // Use XBuild
      XBuild(solutionpath, settings =>
        settings.SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information($"Pattern: {testtarget}");
    NUnit3($"./src/**/bin/{configuration}/{testtarget}.dll", new NUnit3Settings { NoResults = true });
});

Task("Create-NuGet-Package")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    var nuGetPackSettings   = new NuGetPackSettings {
        Id                      = "SquirrelCakeStartUp",
        Version                 = nugetVersion,
        Title                   = "Squirrel Cake StartUp",
        Authors                 = new[] {"Lukkian"},
        Description             = "My package description.",
        IconUrl                 = new Uri("file:///" + MakeAbsolute(File($"./src/{mainproject}/cake.ico")).FullPath),
        ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos", version},
        Files                   = new [] {
                                    new NuSpecContent {Source = "DeltaCompressionDotNet.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "DeltaCompressionDotNet.MsDelta.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "DeltaCompressionDotNet.PatchApi.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Mono.Cecil.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Mono.Cecil.Mdb.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Mono.Cecil.Pdb.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Mono.Cecil.Rocks.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "NuGet.Squirrel.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "SharpCompress.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Splat.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = "Squirrel.dll", Target = @"lib\net45"},
                                    new NuSpecContent {Source = $"{mainproject}.exe", Target = @"lib\net45"},
                                    new NuSpecContent {Source = $"{mainproject}.exe.config", Target = @"lib\net45"},
                                },
        BasePath                = $"./src/{mainproject}/bin/release",
        OutputDirectory         = $"{artifacts}/nuget"
    };

    NuGetPack($"./src/{mainproject}/SquirrelCakeStartUp.nuspec", nuGetPackSettings);
});

Task("Squirrel")
    .IsDependentOn("Create-NuGet-Package")
	.Does(() => 
{
    Squirrel(File($"{artifacts}/nuget/SquirrelCakeStartUp.{nugetVersion}.nupkg"));
    Information($"Squirrel package for version {nugetVersion} created on folder ./Releases/");
});

Task("Publish-ClickOnce")
    .IsDependentOn("Squirrel")
    .Does(() =>
{
    PublishApp(mainprojectpath)
    	.SetConfiguration(configuration)
        //.SetBuildPlatform(MSBuildPlatform.x86)
        //.SetBuildPlatform(MSBuildPlatform.x64)
        //.SetBuildPlatform(MSBuildPlatform.AnyCPU)
        //.ForceRebuild()
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
