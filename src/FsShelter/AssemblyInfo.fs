namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FsShelter")>]
[<assembly: AssemblyProductAttribute("FsShelter")>]
[<assembly: AssemblyDescriptionAttribute("F# DSL and runtime for Storm topologies")>]
[<assembly: AssemblyVersionAttribute("0.0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.2"
