#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0
#tool nuget:?package=GitVersion.CommandLine&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=12.0.1
#tool nuget:?package=gitreleasemanager&version=0.8.0

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

//.\build.ps1 --configuration="Debug"

var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solution = "Cake.StartUp";
var mainproject = "WindowsFormsApp";
var testtarget = "*tests";
var artifacts = "./artifacts";
var mainprojectpath = $"./src/{mainproject}/{mainproject}.csproj";
var solutionpath = $"./src/{solution}.sln";
var publishpath = $"./{artifacts}/publish";
GitVersion gitVersion = null;

private const string gh_token = "fd70321c54790b0edb10cfdf161d7c5ee23516f5";
private const string gh_owner = "Lukkian";
private const string gh_repo = "Cake.StartUp";
private const string gh_branch = "master";
private string grm_log = $"{artifacts}/GitReleaseManager.log";
private string releaseFiles = null;

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var solutionObj = ParseSolution(solutionpath);
var projects = solutionObj.Projects;
var projectPaths = projects.Select(p => p.Path.GetDirectory());
//var testAssemblies = projects.Where(p => p.Name.Contains("Tests")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
//var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));

//////////////////////////////////////////////////////////////////////
// CUSTOM FUNCTIONS
//////////////////////////////////////////////////////////////////////

private bool IsReleaseMode() => string.Equals(configuration, "Release", StringComparison.InvariantCultureIgnoreCase);
//private bool IsReleaseMode() => AppVeyor.IsRunningOnAppVeyor && AppVeyor.Environment.Repository.Tag.IsTag;
private bool ShouldPatchAssemblyInfo() => AppVeyor.IsRunningOnAppVeyor;
private bool ShouldPublishReleaseOnGitHub() => AppVeyor.IsRunningOnAppVeyor && string.Equals(gitVersion?.BranchName, gh_branch, StringComparison.InvariantCultureIgnoreCase);

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	Information($"Solution: {solutionpath}");
	Information($"Main project: {mainprojectpath}");
    Information($"Mode: {configuration}");
});

//////////////////////////////////////////////////////////////////////
// TEARDOWN
//////////////////////////////////////////////////////////////////////

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");

    if(gitVersion != null)
    {
        Warning("****************************************");
        Warning($"NugetVersion: {gitVersion.NuGetVersionV2}");
        Warning($"FullSemVer: {gitVersion.FullSemVer}");
        Warning($"InformationalVersion: {gitVersion.InformationalVersion}");
        Warning("****************************************");
    }
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
        Information($"Cleaning {path}/(bin|obj)");
        CleanDirectories($"{path}/**/bin");
        CleanDirectories($"{path}/**/obj");
    }
    Information($"Cleaning: {artifacts}/releases");
    CleanDirectory($"{artifacts}/releases");
    Information($"Cleaning: {artifacts}/nuget");
    CleanDirectory($"{artifacts}/nuget");
});

Task("RunGitVersion")
    .Does(() =>
{
    gitVersion = GitVersion(new GitVersionSettings {
        RepositoryPath = ".",
        LogFilePath = $"{artifacts}/GitVersion.log",
        OutputType = GitVersionOutput.Json,
        UpdateAssemblyInfo = ShouldPatchAssemblyInfo()
    });
    
    Information($"Full GitVersion: {Newtonsoft.Json.JsonConvert.SerializeObject(gitVersion)}");
});

Task("CheckAndUpdateAppVeyorBuild")
    .IsDependentOn("RunGitVersion")
    .Does(() =>
{
    if (AppVeyor.IsRunningOnAppVeyor)
    {
        StartPowershellFile("./appveyor.ps1", args => { args.Append("Version", $"{gitVersion.FullSemVer}"); });
        Information($"AppVeyor Info");
        Information($"    Folder: {AppVeyor.Environment.Build.Folder}");
        Information($"    Number: {AppVeyor.Environment.Build.Number}");
    }
    else
    {
        Information("Not running on AppVeyor");
    }
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
    .IsDependentOn("CheckAndUpdateAppVeyorBuild")
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
    .IsDependentOn("RunGitVersion")
    .WithCriteria(IsReleaseMode())
    .Does(() =>
{
    var nuGetPackSettings   = new NuGetPackSettings {
        Id                      = mainproject.Replace(".", string.Empty),
        Version                 = gitVersion.NuGetVersionV2,
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
        ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos", $"{gitVersion.NuGetVersionV2} release notes"},
        BasePath                = $"./src/{mainproject}/bin/{configuration}",
        OutputDirectory         = $"{artifacts}/nuget"
    };

    NuGetPack($"./src/{mainproject}/{mainproject}.nuspec", nuGetPackSettings);
});

Task("SquirrelPackage")
    .IsDependentOn("NuGetPackage")
    .IsDependentOn("RunGitVersion")
    .WithCriteria(IsReleaseMode())
	.Does(() => 
{
    var squirrelSettings = new SquirrelSettings {
        LoadingGif = $"./src/{mainproject}/loading.gif",
        ReleaseDirectory = $"{artifacts}/releases",
        FrameworkVersion = "net472",
    };
    Squirrel(File($"{artifacts}/nuget/{mainproject}.{gitVersion.NuGetVersionV2}.nupkg"), squirrelSettings);
    Information($"Squirrel package for version {gitVersion.NuGetVersionV2} created on folder: {squirrelSettings.ReleaseDirectory}");

    var files = GetFiles($"{artifacts}/releases/*");
    foreach(var file in files)
    {
        Information("   File: {0}", file);
    }
    releaseFiles = files.Select(f => f.GetFilename()).Aggregate((a, b) => $"{a},{b}").ToString();
});

Task("CreateReleaseNotes")
    .IsDependentOn("SquirrelPackage")
    .IsDependentOn("RunGitVersion")
    .WithCriteria(ShouldPublishReleaseOnGitHub())
    .Does(() =>
{
    GitReleaseManagerCreate(gh_token, gh_owner, gh_repo, new GitReleaseManagerCreateSettings {
        //Milestone         = gitVersion.SemVer,
        Name              = $"v{gitVersion.SemVer}",
        InputFilePath     = "RELEASENOTES.md",
        Prerelease        = false,
        TargetCommitish   = "master",
        WorkingDirectory  = $"{artifacts}/releases",
        ToolTimeout       = TimeSpan.FromSeconds(120),
        LogFilePath       = grm_log
    });
});

Task("AttachArtifact")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("CreateReleaseNotes")
    .WithCriteria(ShouldPublishReleaseOnGitHub())
    .Does(() =>
{
    GitReleaseManagerAddAssets(gh_token, gh_owner, gh_repo, $"v{gitVersion.SemVer}",
        releaseFiles,
        new GitReleaseManagerAddAssetsSettings {
            WorkingDirectory  = $"{artifacts}/releases",
            ToolTimeout       = TimeSpan.FromSeconds(120),
            LogFilePath       = grm_log
        });
    Information("Files attached to the release: {0}", releaseFiles);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("AttachArtifact");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
