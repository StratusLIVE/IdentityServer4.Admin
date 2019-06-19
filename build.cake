#tool "nuget:https://api.nuget.org/v3/index.json?package=GitVersion.CommandLine"

var target          = Argument("target", "Publish");
var configuration   = Argument<string>("configuration", "Release");

 var versionOverride = Argument<string>("release", "");
 var suffixOverride = Argument<string>("pre", "");
///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var outputPath            = Directory("./src/out");
var buildArtifacts      = Directory("./artifacts");

var isWindows           = IsRunningOnWindows();

DotNetCoreMSBuildSettings msBuildSettings;
VersionInfo versions;

///////////////////////////////////////////////////////////////////////////////
// Setup
///////////////////////////////////////////////////////////////////////////////
class VersionInfo
{
    public string AssemblyVersion { get; set; }
    public string VersionSuffix { get; set; }
    public string FileVersion { get; set; }
    public string InformationalVersion { get; set; }
    public string BranchName { get; set; }
    public string PreReleaseLabel { get; set; }
}

// Setup(context =>
// {
//     // only calculate versions if on Windows
//     // due to problems with GitVersion in the current setup - but also since Windows is our release platform anyways
//     if (isWindows)
//     {
//         var gitVersions = Context.GitVersion();
        
//         versions = new VersionInfo
//         {
//             InformationalVersion = gitVersions.InformationalVersion,
//             BranchName = gitVersions.BranchName,
//             PreReleaseLabel = gitVersions.PreReleaseLabel
//         };

//         // explicit version has been passed in as argument
//         if (!string.IsNullOrEmpty(versionOverride))
//         {
//             versions.AssemblyVersion = versionOverride;
//             versions.FileVersion = versionOverride;

//             if (!string.IsNullOrEmpty(suffixOverride))
//             {
//                 versions.VersionSuffix = suffixOverride;
//             }
//         }
//         else
//         {
//             versions.AssemblyVersion = gitVersions.AssemblySemVer;
//             versions.FileVersion = gitVersions.AssemblySemVer;

//             if (!string.IsNullOrEmpty(versions.PreReleaseLabel))
//             {
//                 versions.VersionSuffix = gitVersions.PreReleaseLabel + gitVersions.CommitsSinceVersionSourcePadded;      
//             }
            
//         }

//         Information("branch            : " + versions.BranchName);
//         Information("pre-release label : " + versions.PreReleaseLabel);
//         Information("version           : " + versions.AssemblyVersion);
//         Information("version suffix    : " + versions.VersionSuffix);
//         Information("informational     : " + versions.InformationalVersion);
        
//         msBuildSettings = GetMSBuildSettings();
//     }
//     else
//     {
//         Information("Skipping version calculation because not on Windows.");
//         msBuildSettings = null;
//     }
// });

///////////////////////////////////////////////////////////////////////////////
// Clean
///////////////////////////////////////////////////////////////////////////////
Task("Clean")
    .Does(() =>
{
    CleanDirectories(new DirectoryPath[] { buildArtifacts });
});

///////////////////////////////////////////////////////////////////////////////
// Restore
///////////////////////////////////////////////////////////////////////////////
Task("Restore")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

///////////////////////////////////////////////////////////////////////////////
// Build
///////////////////////////////////////////////////////////////////////////////
Task("Build")    
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings 
    {
        Configuration = configuration,
        MSBuildSettings = msBuildSettings,
        //ArgumentCustomization = args => args.Append("--no-restore")
    };

    var projects = GetFiles("./src/**/*.csproj");
    foreach(var project in projects)
	{
	    DotNetCoreBuild(project.GetDirectory().FullPath, settings);
    }

    if (!isWindows)
    {
        Information("Not running on Windows - skipping building tests for .NET Framework");
        settings.Framework = "netcoreapp2.1";
    }

    var tests = GetFiles("./test/**/*.csproj");
    foreach(var test in tests)
	{
	    DotNetCoreBuild(test.GetDirectory().FullPath, settings);
    }
});

///////////////////////////////////////////////////////////////////////////////
// Test
///////////////////////////////////////////////////////////////////////////////
Task("Test")
    .Does(() =>
{
    var settings = new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true
    };

    if (!isWindows)
    {
        Information("Not running on Windows - skipping tests for .NET Framework");
        settings.Framework = "netcoreapp2.1";
    }

    var projects = GetFiles("./test/**/*.csproj");
    foreach(var project in projects)
    {
        DotNetCoreTest(project.FullPath, settings);
    }
});

///////////////////////////////////////////////////////////////////////////////
// Publish
///////////////////////////////////////////////////////////////////////////////
Task("Publish")
    .Does(() =>
    {
        var projects = GetFiles("./src/**/*.csproj");
        foreach(var project in projects)
        {
            DotNetCorePublish(
                project.FullPath,
                new DotNetCorePublishSettings()
                {
                    Configuration = configuration,
                    OutputDirectory = outputPath,
                    ArgumentCustomization = args => args
                    .Append("--no-restore")
                    //.Append("--self-contained"),
                });
        }     
    });

private DotNetCoreMSBuildSettings GetMSBuildSettings()
{
    var settings = new DotNetCoreMSBuildSettings();

    settings.WithProperty("AssemblyVersion", versions.AssemblyVersion);
    settings.WithProperty("VersionPrefix", versions.AssemblyVersion);
    settings.WithProperty("FileVersion", versions.FileVersion);
    settings.WithProperty("InformationalVersion", versions.InformationalVersion);
    
    if (!String.IsNullOrEmpty(versions.VersionSuffix))
    {
        settings.WithProperty("VersionSuffix", versions.VersionSuffix);
    }

    return settings;
}

RunTarget(target);