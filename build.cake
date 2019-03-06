#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0

// Squirrel: It's like ClickOnce but Works
#tool nuget:?package=squirrel.windows&version=1.9.1
#addin nuget:?package=Cake.Squirrel&version=0.14.0

// To debug in dotnet core [preliminar support], otherwise use: #addin nuget:?package=Cake.Powershell&version=0.4.7
//#addin nuget:?package=Lukkian.Cake.Powershell&version=0.4.9
#addin nuget:?package=Cake.Powershell&version=0.4.7

// To debug in VSCode uncomment below lines
// #r ".\tools\Addins\Lukkian.Cake.Powershell.0.4.9\lib\netcoreapp2.1\Cake.Core.Powershell.dll"

//////////////////////////////////////////////////////////////////////
// Fetch the most recent bootstrapper file from the resources repository using:
//////////////////////////////////////////////////////////////////////

// Invoke-WebRequest https://cakebuild.net/download/bootstrapper/windows -OutFile build.ps1

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

//.\build.ps1 --appversion="1.0.0"
//.\build.ps1 --appversion="1.0.0" --forceversion=true

const string defaultVersion = "1.0.0";
var appversion = Argument("appversion", defaultVersion);
var forceversion = Argument("forceversion", false);
var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var version = appversion;
var solution = "Cake.StartUp";
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
    
    if(forceversion || version != defaultVersion)
    {
        Warning($"AppVersion: {version} (forced: {forceversion})");
    }

    Information("Revision number will always be ignored and reseted to zero.");

    // Versioning
    var publishFile = "./publish.props";
    if (FileExists(publishFile) == false)
        throw new Exception("Build aborted: file publish.props not found!");
    Information($"ClickOnce props file: {publishFile}");

    var storedVersion = XmlPeek(publishFile, "//ApplicationVersion");
    var previousVersion = new Version(storedVersion);
    Information($"Previous version: {previousVersion}");

    var currentVersion = new Version(previousVersion.Major, previousVersion.Minor, previousVersion.Build + 1);

    var versionArg = new Version(version);
    
    if(forceversion || versionArg > currentVersion)
    {
        currentVersion = versionArg;
    }

    if(currentVersion.Major < 1) { currentVersion = new Version($"1.{currentVersion.Minor}.{currentVersion.Build}"); }
    if(currentVersion.Minor < 0) { currentVersion = new Version($"{currentVersion.Major}.0.{currentVersion.Build}"); }
    if(currentVersion.Build < 0) { currentVersion = new Version($"{currentVersion.Major}.{currentVersion.Minor}.0"); }
    if(currentVersion.Revision >= 0) { currentVersion = new Version($"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}"); }

    if(currentVersion <= previousVersion)
    {
        Warning($"Next version: {currentVersion} is smaller or equal to the previous version: {previousVersion}");
    }

    version = currentVersion.ToString();
    nugetVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

    Information($"Current version: {currentVersion} (forced: {forceversion})");

    XmlPoke(publishFile, "//ApplicationVersion", currentVersion.ToString());

    var nextVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build + 1);
    Information($"Next version: {nextVersion}");

    // Set the version in all the AssemblyInfo.cs or AssemblyInfo.vb files in any subdirectory
    Information("Patching AssemblyInfo with new version number...");
    //StartPowershellFile("./SetAssemblyInfoVersion.ps1", args => { args.Append("Version", $"{version}.0"); });
    StartPowershellScript("./SetAssemblyInfoVersion.ps1", new PowershellSettings { OutputToAppConsole = false }
        .WithArguments(args => { args.Append("Version", $"{version}.0"); }));
    Information($"AssemblyInfo version patched to: {version}.0");
    if (AppVeyor.IsRunningOnAppVeyor)
    {
        StartPowershellFile("./appveyor.ps1", args => { args.Append("Version", $"{version}"); });
        Information(
            $@"AppVeyor Info:
                Folder: {AppVeyor.Environment.Build.Folder}
                Configuration: {AppVeyor.Environment.Configuration}
                Platform: {AppVeyor.Environment.Platform}
                Id: {AppVeyor.Environment.Build.Id}
                Number: {AppVeyor.Environment.Build.Number}
                Version: {AppVeyor.Environment.Build.Version}"
        );
    }
    else
    {
        Information("Not running on AppVeyor");
    }
});

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
    Information("****************************************");
    Warning($"Current published version: {version}");
    Information("****************************************");
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
    Information($"Cleaning: {artifacts}/releases");
    CleanDirectory($"{artifacts}/releases");
});

Task("RestoreNuGetPackages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    // Restore all NuGet packages.
    Information("Restore all NuGet packages...");
    NuGetRestore(solutionpath);
});

Task("Build")
    .IsDependentOn("RestoreNuGetPackages")
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

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information($"Pattern: {testtarget}");
    NUnit3($"./src/**/bin/{configuration}/{testtarget}.dll", new NUnit3Settings { NoResults = true });
});

Task("NuGetPackage")
    .IsDependentOn("UnitTests")
    .Does(() =>
{
    var nuGetPackSettings   = new NuGetPackSettings {
        Id                      = mainproject.Replace(".", string.Empty),
        Version                 = nugetVersion,
        Verbosity               = NuGetVerbosity.Detailed,
        Title                   = "Windows Forms App",
        Authors                 = new[] {"Lukkian"},
        Description             = "My app description.",
        IconUrl                 = new Uri("file:///" + MakeAbsolute(File($"./src/{mainproject}/cake.ico")).FullPath),
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
        ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos", $"{version} release notes"},
        BasePath                = $"./src/{mainproject}/bin/{configuration}",
        OutputDirectory         = $"{artifacts}/nuget"
    };

    NuGetPack($"./src/{mainproject}/{mainproject}.nuspec", nuGetPackSettings);
});

Task("SquirrelPackage")
    .IsDependentOn("NuGetPackage")
	.Does(() => 
{
    var squirrelSettings = new SquirrelSettings {
        LoadingGif = $"./src/{mainproject}/loading.gif",
        ReleaseDirectory = $"{artifacts}/releases",
        FrameworkVersion = "net472",
    };
    Squirrel(File($"{artifacts}/nuget/{mainproject}.{nugetVersion}.nupkg"), squirrelSettings);
    Information($"Squirrel package for version {nugetVersion} created on folder: {squirrelSettings.ReleaseDirectory}");
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("SquirrelPackage");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
