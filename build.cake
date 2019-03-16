#tool nuget:?package=NUnit.ConsoleRunner&version=3.9.0
#tool nuget:?package=GitVersion.CommandLine&version=4.0.0
#tool nuget:?package=gitreleasemanager&version=0.8.0
#tool nuget:?package=squirrel.windows&version=1.9.1
#addin nuget:?package=Cake.Squirrel&version=0.14.0
#addin nuget:?package=Newtonsoft.Json&version=12.0.1
#addin nuget:?package=Cake.FileHelpers&version=3.1.0
#addin nuget:?package=Cake.Git&version=0.19.0

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
// HELP
//////////////////////////////////////////////////////////////////////

//.\build.ps1 --configuration="Debug"
//.\build.ps1 --configuration="Debug" --publish=true

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var configuration = Argument("configuration", "Release");
var target = Argument("target", "Default");
var publish = Argument("publish", (string)null);

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solution = "Cake.StartUp";
var main_project_name = "WindowsFormsApp";
var app_uri_icon = "https://raw.githubusercontent.com/Lukkian/Cake.StartUp/master/src/WindowsFormsApp/cake.ico";
// Nuget/Squirrel uninstall icon must be on a public Webserver and its fetched at installation time not packaging time.
// See: https://github.com/Squirrel/Squirrel.Windows/issues/761 and https://github.com/NuGet/Home/issues/352
var app_local_icon = MakeAbsolute(File($"./src/{main_project_name}/cake.ico")).FullPath;
var app_install_gif = $"./src/{main_project_name}/loading.gif";
var app_install_description = "A simple Cake StartUp sample wtih Squirrel.Windows intallation and update system.";
var test_target = "*tests";
var artifacts = "./artifacts";
var solution_path = $"./src/{solution}.sln";
var local_release_dir = @"C:\MyAppUpdates";

var gh_owner = "Lukkian";
var gh_repo = "Cake.StartUp";
var release_branch = "master";
var gh_token = EnvironmentVariable("gh_token") ?? Argument("gh_token", (string)null);
var grm_log = $"{artifacts}/GitReleaseManager.log";
var release_files = (string)null;
var tool_timeout = TimeSpan.FromMinutes(5);

var git_version = (GitVersion)null;
var git_version_settings = new GitVersionSettings {
    RepositoryPath = ".",
    LogFilePath = $"{artifacts}/GitVersion.log",
    OutputType = GitVersionOutput.Json,
    UpdateAssemblyInfo = false
};

//////////////////////////////////////////////////////////////////////
// CUSTOM FUNCTIONS
//////////////////////////////////////////////////////////////////////

bool IsReleaseMode() => StringComparer.OrdinalIgnoreCase.Equals(configuration, "Release");
bool IsReleaseBranch()
{
    var branch = git_version?.BranchName.ToLowerInvariant();

    switch (branch)
    {
        case"master": return true;
        case"beta": return true;
        case"develop": return false;
        default: return false;
    }
}
bool ShouldPatchAssemblyInfo() => true;
bool ShouldResetAssemblyInfo() => BuildSystem.IsLocalBuild;
bool ShouldPublishRelease()
{
    var haveVersion = git_version != null;
    
    var isInReleaseBranch = IsReleaseBranch();

    if(haveVersion && isInReleaseBranch)
    {
        var gitDescribe = GitDescribe("./", git_version.Sha, false, GitDescribeStrategy.All, 0);
        
        if (gitDescribe.StartsWith("tags/"))
        {
            return true;
        }
    }

    return false;
}
bool ShouldPublishReleaseLocal()
{
    var shouldPublishReleaseLocal = ShouldPublishRelease();

    if(shouldPublishReleaseLocal)
    {
        if(BuildSystem.IsLocalBuild)
        {
            return true;
        }
    }

    return false;
}
bool ShouldPublishReleaseOnGitHub()
{
    var shouldPublishReleaseLocal = ShouldPublishRelease();

    if(shouldPublishReleaseLocal)
    {
        if(AppVeyor.IsRunningOnAppVeyor && AppVeyor.Environment.Repository.Tag.IsTag)
        {
            return true;
        }
    }

    return false;
}
bool HaveGitHubCredentials()
{
    var haveGHC = !string.IsNullOrWhiteSpace(gh_owner);
    haveGHC = haveGHC && !string.IsNullOrWhiteSpace(gh_repo);
    haveGHC = haveGHC && !string.IsNullOrWhiteSpace(gh_token);
    return haveGHC;
}
bool HasErrors() => Context.Data.Get<BuildData>().HasError;

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup<BuildData>(ctx =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
	Information($"Solution: {solution_path}");
	Information($"Main project: ./src/{main_project_name}/{main_project_name}.csproj");
    Information($"Mode: {configuration}");

    var lastCommit = GitLogTip("./");

    Information(
        @"Last commit {0}
        Short message: {1}
        Author:        {2}
        Authored:      {3:yyyy-MM-dd HH:mm:ss}
        Committer:     {4}
        Committed:     {5:yyyy-MM-dd HH:mm:ss}",
        lastCommit.Sha,
        lastCommit.MessageShort,
        lastCommit.Author.Name,
        lastCommit.Author.When,
        lastCommit.Committer.Name,
        lastCommit.Committer.When
    );

    return new BuildData();
});

//////////////////////////////////////////////////////////////////////
// TEARDOWN
//////////////////////////////////////////////////////////////////////
Teardown<BuildData>((ctx, data) =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");

    Warning("To create a release everything below should be True except HasErros");
    Warning("ShouldPublishReleaseOnGitHub: {0}", ShouldPublishReleaseOnGitHub());
    Warning("HaveGitHubCredentials: {0}",        HaveGitHubCredentials());
    Warning("IsReleaseMode: {0}",                IsReleaseMode());
    Warning("HasErrors: {0}",                    HasErrors());

    if(git_version != null)
    {
        Warning("****************************************");
        Warning($"NugetVersion: {git_version.NuGetVersionV2}");
        Warning($"FullSemVer: {git_version.FullSemVer}");
        Warning($"InformationalVersion: {git_version.InformationalVersion}");
        Warning("****************************************");
    }
    else
    {
        Error("****************************************");
        Error($"GitVersion is null");
        Error("****************************************");
    }

    if(data.ReleaseNotes.Any())
    {
        Information("Release notes:");
        foreach(var releaseNote in data.ReleaseNotes)
        {
            Information($"  >{releaseNote}");
        }
    }

    if (data.HasError)
    {
        Error("There were one or more errors while executing the build tasks");
        foreach(var error in data.Errors)
        {
            Error($"  >{error}");
        }
    }
    else
    {
        Information("There were no errors in BuildData");
    }
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    var solutionObj = ParseSolution(solution_path);
    var projects = solutionObj.Projects;
    var projectPaths = projects.Select(p => p.Path.GetDirectory());
    //var testAssemblies = projects.Where(p => p.Name.Contains("Tests")).Select(p => p.Path.GetDirectory() + "/bin/" + configuration + "/" + p.Name + ".dll");
    //var testResultsPath = MakeAbsolute(Directory(artifacts + "./test-results"));

    // Clean solution directories
    foreach(var path in projectPaths)
    {
        Information($"Cleaning {path}/(bin|obj)");
        CleanDirectories($"{path}/**/bin");
        CleanDirectories($"{path}/**/obj");
    }

    // Clean artifacts directories
    Information($"Cleaning: {artifacts}/releases");
    CleanDirectory($"{artifacts}/releases");
    Information($"Cleaning: {artifacts}/nuget");
    CleanDirectory($"{artifacts}/nuget");
});

Task("CreateReleaseTag")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    git_version = GitVersion(git_version_settings);

    var theTag = $"v{git_version.SemVer}";

    var tags = GitTags("./");

    if(tags.Exists(t => string.Equals(t.FriendlyName, theTag, StringComparison.OrdinalIgnoreCase)))
    {
        Warning($"tag already exists in this reporsitory: {theTag}");

        foreach (var t in tags)
        {
            Information($"tag: {t.FriendlyName}");
        }

        return;
    }

    Information($"Tagging current commit with value {theTag}");
    
    GitTag(@"./", theTag, git_version.Sha);

    git_version = GitVersion(git_version_settings);
});

Task("PushReleaseTag")
    .IsDependentOn("CreateReleaseTag")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    var gitUser = "gitUser";
    var gitPassword = "gitPassword";

    var theTag = $"v{git_version.SemVer}";

    Information($"Push tag {theTag} to remote repository");
    
    // Push a tag to a remote authenticated
    GitPushRef(@"./", gitUser, gitPassword, "origin", theTag);

    // Push a tag to a remote unauthenticated
    //GitPushRef(@"./", "origin", theTag);
});

Task("CheckCurrentCommitTag")
    .Does(() =>
{
    git_version = GitVersion(git_version_settings);

    var gitDescribe = GitDescribe("./", git_version.Sha, false, GitDescribeStrategy.All, 0);
    
    Information($"{git_version.Sha} HasTag: {gitDescribe.StartsWith("tags/")} - {gitDescribe}");
});

Task("RunGitVersion")
    .Does(() =>
{
    git_version_settings.UpdateAssemblyInfo = ShouldPatchAssemblyInfo();

    git_version = GitVersion(git_version_settings);

    git_version_settings.UpdateAssemblyInfo = false;
    
    Information($"GitVersion: {Newtonsoft.Json.JsonConvert.SerializeObject(git_version)}");

    if (AppVeyor.IsRunningOnAppVeyor)
    {
        AppVeyor.UpdateBuildVersion($"{git_version.FullSemVer}.b{AppVeyor.Environment.Build.Number}");
        Information($"AppVeyor.Environment.Build.Version: {AppVeyor.Environment.Build.Version}");
    }
});

Task("RestoreNuGetPackages")
    .Does(() =>
{
    NuGetRestore(solution_path);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("RestoreNuGetPackages")
    .Does(() =>
{
	Information("Building solution...");
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solution_path, settings =>
        settings.SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
    }
    else
    {
      // Use XBuild
      XBuild(solution_path, settings =>
        settings.SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
    }
});

Task("ResetAssemblyInfoVersion")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("Build")
    .WithCriteria(() => ShouldResetAssemblyInfo())
    .Does(() =>
{
    StartPowershellFile("./SetAssemblyInfoVersion.ps1", new PowershellSettings() { OutputToAppConsole = false }
        .WithArguments(args =>
        {
            args.Append("Version", $"{git_version.Major}.{git_version.Minor}.0.0");
        }));
});

Task("UnitTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information($"Pattern: {test_target}");
    
    NUnit3($"./src/**/bin/{configuration}/{test_target}.dll", new NUnit3Settings { NoResults = false, OutputFile = "./TestResult.xml" });
    
    if (AppVeyor.IsRunningOnAppVeyor)
    {
        AppVeyor.UploadTestResults("./TestResult.xml", AppVeyorTestResultsType.NUnit3);
    }
});

Task("ReadReleaseNotes")
    .WithCriteria(() => IsReleaseMode())
	.Does(() => 
{
    string[] releaseNotes;

    if(FileExists("./RELEASENOTES.md"))
    {
        releaseNotes = FileReadLines("./RELEASENOTES.md");
    }
    else
    {
        releaseNotes = new [] { "No release notes found." };
    }
    
    var data = Context.Data.Get<BuildData>();
    data.SetReleaseNotes(releaseNotes);

    Information("Release notes processed, see teardown...");
});

Task("NuGetPackage")
    .IsDependentOn("UnitTests")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("ReadReleaseNotes")
    .WithCriteria(() => IsReleaseMode())
    .Does(() =>
{
    var nuGetPackSettings = new NuGetPackSettings {
        Id                      = main_project_name.Replace(".", string.Empty),
        Version                 = git_version.NuGetVersionV2,
        Verbosity               = NuGetVerbosity.Detailed,
        Title                   = $"{main_project_name}-{git_version.SemVer}",
        Authors                 = new[] {"Lukkian"},
        Description             = app_install_description,
        IconUrl                 = new Uri(app_uri_icon),
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
            new NuSpecContent {Source = $"{main_project_name}.exe", Target = @"lib\net45"},
            new NuSpecContent {Source = $"{main_project_name}.exe.config", Target = @"lib\net45"},
        },
        ReleaseNotes            = Context.Data.Get<BuildData>().ReleaseNotes,
        BasePath                = $"./src/{main_project_name}/bin/{configuration}",
        OutputDirectory         = $"{artifacts}/nuget"
    };

    NuGetPack($"./src/{main_project_name}/{main_project_name}.nuspec", nuGetPackSettings);
});

Task("SquirrelPackage")
    .IsDependentOn("NuGetPackage")
    .IsDependentOn("RunGitVersion")
    .WithCriteria(() => IsReleaseMode())
	.Does(() => 
{
    var squirrelSettings = new SquirrelSettings {
        Icon             = app_uri_icon,
        SetupIcon        = app_local_icon,
        LoadingGif       = app_install_gif,
        ReleaseDirectory = $"{artifacts}/releases",
        FrameworkVersion = "net472",
        NoMsi            = true,
        NoDelta          = true,
    };
    Squirrel(File($"{artifacts}/nuget/{main_project_name.Replace(".", string.Empty)}.{git_version.NuGetVersionV2}.nupkg"), squirrelSettings);
    Information($"Squirrel package for version {git_version.NuGetVersionV2} created on folder: {squirrelSettings.ReleaseDirectory}");

    if(FileExists($"{artifacts}/releases/Setup.exe"))
    {
        MoveFile($"{artifacts}/releases/Setup.exe", $"{artifacts}/releases/{main_project_name}-{git_version.SemVer}.exe");
    }

    var files = GetFiles($"{artifacts}/releases/*");
    foreach(var file in files)
    {
        Information("   File: {0}", file);
    }
    release_files = files.Select(f => f.GetFilename()).Aggregate((a, b) => $"{a},{b}").ToString();
});

Task("CreateLocalRelease")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("SquirrelPackage")
    .WithCriteria(() => ShouldPublishReleaseLocal())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    Information($"Check source directory: {artifacts}/releases");
    Information($"Check target directory: {local_release_dir}");
    if (DirectoryExists(local_release_dir))
    {
        Information("Coping files...");
        CopyFiles($"{artifacts}/releases/*", local_release_dir);
    }
    Information($"Release pushed to directory: {local_release_dir}");
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

Task("CreateGitHubRelease")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("SquirrelPackage")
    .WithCriteria(() => ShouldPublishReleaseOnGitHub())
    .WithCriteria(() => HaveGitHubCredentials())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    GitReleaseManagerCreate(gh_token, gh_owner, gh_repo, new GitReleaseManagerCreateSettings {
        //Milestone         = gitVersion.SemVer,
        Name              = $"v{git_version.SemVer}",
        InputFilePath     = "./RELEASENOTES.md",
        Prerelease        = !string.IsNullOrWhiteSpace(git_version.PreReleaseTag),
        TargetCommitish   = git_version.BranchName,
        WorkingDirectory  = $"{artifacts}/releases",
        ToolTimeout       = tool_timeout,
        LogFilePath       = grm_log
    });
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

Task("ExportGitHubReleaseNotes")
    .IsDependentOn("CreateGitHubRelease")
    .WithCriteria(() => ShouldPublishReleaseOnGitHub())
    .WithCriteria(() => HaveGitHubCredentials())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    if(FileExists("./GLOBALRELEASENOTES.md"))
    {
        DeleteFile("./GLOBALRELEASENOTES.md");
    }

    GitReleaseManagerExport(gh_token, gh_owner, gh_repo, File("./GLOBALRELEASENOTES.md"), new GitReleaseManagerExportSettings {
        ToolTimeout       = tool_timeout,
        LogFilePath       = grm_log
    });
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

Task("AttachGitHubReleaseArtifacts")
    .IsDependentOn("RunGitVersion")
    .IsDependentOn("SquirrelPackage")
    .IsDependentOn("CreateGitHubRelease")
    .IsDependentOn("ExportGitHubReleaseNotes")
    .WithCriteria(() => ShouldPublishReleaseOnGitHub())
    .WithCriteria(() => HaveGitHubCredentials())
    .WithCriteria(() => HasErrors() == false)
    .Does(() =>
{
    var globalReleaseNotesFile = MakeAbsolute(File("./GLOBALRELEASENOTES.md")).FullPath;
    release_files += $",{globalReleaseNotesFile}";
    GitReleaseManagerAddAssets(gh_token, gh_owner, gh_repo, $"v{git_version.SemVer}", release_files,
        new GitReleaseManagerAddAssetsSettings {
            WorkingDirectory  = $"{artifacts}/releases",
            ToolTimeout       = tool_timeout,
            LogFilePath       = grm_log
    });

    Information($"Files attached to the release: (WorkingDirectory: {artifacts}/releases)");
    foreach (var file in release_files.Split(','))
    {
        Information($"    >{file}");
    }
}).OnError(exception => {
    Error(exception);
    var data = Context.Data.Get<BuildData>();
    data.HasError = true;
    data.AddError(exception.Message);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("CreateLocalRelease")
    .IsDependentOn("ResetAssemblyInfoVersion")
    .IsDependentOn("AttachGitHubReleaseArtifacts");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

public class BuildData
{
    public bool HasError { get; set; }
    public IList<string> Errors { get => _errors; }
    private IList<string> _errors;
    public ICollection<string> ReleaseNotes { get => _releaseNotes; }
    private ICollection<string> _releaseNotes;

    public BuildData()
    {
        _errors = new List<string>();
        _releaseNotes = new List<string>();
    }

    public void AddError(string error)
    {
        _errors.Add(error);
    }

    public void SetReleaseNotes(ICollection<string> releaseNotes)
    {
        _releaseNotes= releaseNotes;
    }
}