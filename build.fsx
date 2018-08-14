open Fake.FileSystem
// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"
#r "System.Web.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO
#if MONO
#else
#load "packages/build/SourceLink.Fake/tools/Fake.fsx"
open SourceLink
#endif

let projects =
    !! "src/**/*.??proj"
    ++ "ext/**/*.??proj"
    ++ "samples/**/*.??proj"


// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "Prolucid" 
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "FsShelter"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/"+gitOwner+"/"+gitName

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then failwithf "dotnet %s failed" args


Target "Clean" (fun _ ->
    let dirs = { BaseDirectory = Path.GetFullPath "."
                 Includes = projects
                            |> Seq.map Path.GetDirectoryName
                            |> Seq.collect (fun n -> [Path.Combine(n,"bin"); Path.Combine(n,"obj")] )
                            |> List.ofSeq
                 Excludes = [] }
                    
    dirs
    ++ "docs/output"
    |> Seq.iter (CleanDir)
)

Target "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<PackageProjectUrl>https://github.com/prolucid/FsShelter</PackageProjectUrl>"
      "<PackageLicenseUrl>https://raw.githubusercontent.com/prolucid/FsShelter/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl>https://raw.githubusercontent.com/prolucid/FsShelter/master/docs/files/img/logo.png</PackageIconUrl>"
      "<RepositoryUrl>https://github.com/prolucid/FsShelter.git</RepositoryUrl>"
      "<PackageTags>storm;cep;event-driven;fsharp;distributed</PackageTags>"
      "<PackageDescription>F# DSL and runtime for Apache Storm topologies</PackageDescription>"
      "<Authors>Prolucid</Authors>"
      sprintf "<PackageReleaseNotes>%s</PackageReleaseNotes>" (List.head release.Notes |> System.Web.HttpUtility.HtmlEncode)
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> WriteToFile false "Directory.Build.props"
)

Target "Restore" (fun _ ->
    projects
    |> Seq.iter ((sprintf "restore %s") >> runDotnet ".")
)

Target "Build" (fun _ ->
    projects
    |> Seq.iter ((sprintf "build -c Release %s") >> runDotnet ".")
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner
Target "Tests" (fun _ ->
    runDotnet "src/FsShelter.Tests" "test --no-restore --filter \"TestCategory!=interactive\""
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "Package" (fun _ ->
    runDotnet 
        "src/FsShelter"
        "pack -c Release"
)

Target "PublishNuget" (fun _ ->
    runDotnet "src/FsShelter" (sprintf "nuget push bin/Release/FsShelter.%s.nupkg -s nuget.org -k %s" release.NugetVersion (environVar "nugetkey"))
)


// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed"
)

let generateHelp' fail debug =
    let args =
        if debug then ["--define:HELP"]
        else ["--define:RELEASE"; "--define:HELP"]
    if executeFSIWithArgs "docs/tools" "generate.fsx" args [] then
        traceImportant "Help generated"
    else
        if fail then
            failwith "generating help documentation failed"
        else
            traceImportant "generating help documentation failed"

let generateHelp fail =
    generateHelp' fail false

Target "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.md"

    generateHelp true
)

Target "GenerateHelpDebug" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp' true true
)

Target "KeepRunning" (fun _ ->    
    use watcher = new FileSystemWatcher(DirectoryInfo("docs/content").FullName,"*.*")
    watcher.EnableRaisingEvents <- true
    watcher.Changed.Add(fun e -> generateHelp false)
    watcher.Created.Add(fun e -> generateHelp false)
    watcher.Renamed.Add(fun e -> generateHelp false)
    watcher.Deleted.Add(fun e -> generateHelp false)

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.EnableRaisingEvents <- false
    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

let createIndexFsx lang =
    let content = """(*** hide ***)
#I build_out

(**
FsShelter ({0})
=========================
*)
"""
    let targetDir = "docs/content" @@ lang
    let targetFile = targetDir @@ "index.fsx"
    ensureDirectory targetDir
    System.IO.File.WriteAllText(targetFile, System.String.Format(content, lang))

Target "AddLangDocs" (fun _ ->
    let args = System.Environment.GetCommandLineArgs()
    if args.Length < 4 then
        failwith "Language not specified."

    args.[3..]
    |> Seq.iter (fun lang ->
        if lang.Length <> 2 && lang.Length <> 3 then
            failwithf "Language must be 2 or 3 characters (ex. 'de', 'fr', 'ja', 'gsw', etc.): %s" lang

        let templateFileName = "template.cshtml"
        let templateDir = "docs/tools/templates"
        let langTemplateDir = templateDir @@ lang
        let langTemplateFileName = langTemplateDir @@ templateFileName

        if System.IO.File.Exists(langTemplateFileName) then
            failwithf "Documents for specified language '%s' have already been added." lang

        ensureDirectory langTemplateDir
        Copy langTemplateDir [ templateDir @@ templateFileName ]

        createIndexFsx lang)
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
    
    // release on github
    createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
    // TODO: |> uploadFile "PATH_TO_FILE"    
    |> releaseDraft
    |> Async.RunSynchronously
)


// --------------------------------------------------------------------------------------
// code-gen tasks
Target "ProtoShell" (fun _ ->
    let generated = "ext" @@ "ProtoShell" @@ "generated" 
    let cli = 
        "packages" @@ "build" @@ "Google.Protobuf.Tools" @@ "tools" 
        @@ if isWindows then "windows_x64" @@ "protoc.exe"
           else if isLinux then "linux_x64" @@ "protoc"
           else "macosx_x64" @@ "protoc"
    CleanDir generated
    Shell.Exec(
            cli,
            "--csharp_out=" + generated 
            + " --proto_path=" + "packages" @@ "build" @@ "Google.Protobuf.Tools" @@ "tools"
            + " --proto_path=" + "paket-files" @@ "prolucid" @@ "protoshell" @@ "src" @@ "main" @@  "proto"
            + " paket-files" @@ "prolucid" @@ "protoshell" @@ "src" @@ "main" @@  "proto" @@ "multilang.proto")
    |> ignore
)

Target "StormThriftNamespace" (fun _ ->
    "paket-files" @@ "et1975" @@ "storm" @@ "storm-core" @@ "src" @@ "storm.thrift"
    |> RegexReplaceInFileWithEncoding "namespace java org.apache.storm.generated" "namespace csharp StormThrift" Text.Encoding.ASCII
)

Target "StormThrift" (fun _ ->
    let generated = "ext" @@ "StormThrift" @@ "StormThrift"
    CleanDir generated
    Shell.Exec(
            "packages" @@ "build" @@ "Thrift" @@ "tools" @@ "thrift-0.9.1.exe",
            "-out " + generated @@ ".."
            + " --gen csharp"
            + " paket-files" @@ "et1975" @@ "storm" @@ "storm-core" @@ "src" @@ "storm.thrift")
    |> ignore
)

Target "GenerateSources" DoNothing

"ProtoShell"
  ==> "GenerateSources"
"StormThriftNamespace"
  ==> "StormThrift"
  ==> "GenerateSources"

// --------------------------------------------------------------------------------------
// graph gen tasks
// GraphViz has to be installed and "dot" be in the path
Target "WordCountSvg" (fun _ ->
    Shell.Exec(
#if MONO
            "mono",
            ""+
#else
            Environment.GetEnvironmentVariable("ComSpec"),
            "/c "+
#endif
            ("samples" @@ "WordCount" @@ "bin" @@ "Release" @@ "WordCount.exe") +
            " graph | dot -Tsvg -o " + "samples/WordCount/obj/WordCount.svg")
    |> ignore
)

Target "GuaranteedSvg" (fun _ ->
    Shell.Exec(
#if MONO
            "mono",
            ""+
#else
            Environment.GetEnvironmentVariable("ComSpec"),
            "/c "+
#endif
            ("samples" @@ "Guaranteed" @@ "bin" @@ "Release" @@ "Guaranteed.exe") +
            " graph | dot -Tsvg -o " + "samples/Guaranteed/obj/Guaranteed.svg")
    |> ignore
)

Target "ExportGraphs" DoNothing
"ExportGraphs"
    <== ["Build";"WordCountSvg";"GuaranteedSvg"]

Target "All" DoNothing
"All"
  <== ["Clean"; "Restore"; "Meta"; "Build"; "Tests"; "Package"; "GenerateReferenceDocs"; "GenerateDocs"]

"Build"
  ==> "Tests"
 
"Build"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"Build"
  ==> "GenerateHelpDebug"

"GenerateHelp"
  ==> "KeepRunning"
    
"Release"
  <== ["All"; "PublishNuget"; "ReleaseDocs"]

RunTargetOrDefault "All"
