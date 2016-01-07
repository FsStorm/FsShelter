#r "System.IO.Compression.dll"
#r "System.IO.Compression.FileSystem.dll"
#r "StormThrift.dll"
#r "Thrift.dll"
open StormThrift
open System


//run a shell command
let shell cmd args workingDir =
    use p = new System.Diagnostics.Process()
    let psi = new System.Diagnostics.ProcessStartInfo()
    match workingDir with Some s -> psi.WorkingDirectory <- s | _ -> ()
    psi.FileName <- cmd
    psi.Arguments <- args
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    p.StartInfo <- psi
    let r = p.Start()
    while not p.StandardOutput.EndOfStream do
        printfn "%s" (p.StandardOutput.ReadLine())
    r


//submit topology to run in storm
//this is done indirectly by executing the built exe
//the exe is run with args: 'submit', nimbus_host, nimbus_port and uploadedJarLocation
//the exe should then build the topology from the DSL spec,
//substitute in some runtime parameters and submit to nimbus
//using the supplied args
let submitTopology exe binDir uploadedJarLocation nimbus_host (nimbus_port:int) =
    let sargs = ["submit"; nimbus_host; nimbus_port.ToString(); uploadedJarLocation]
    let pgm,args = 
        if isMono() then
            "mono", exe::sargs
        else
            exe,sargs
    let args = String.Join(" ", args)
    printfn "submitting: %s" exe
    printfn "with args: %A" args
    shell pgm args (Some binDir)

let submitTopologies binDir uploadedJarLocation nimbus_host (nimbus_port:int) =
    Directory.GetFiles (binDir)
    |> Seq.filter (fun f -> let e = Path.GetExtension(f) in e = ".exe" )
    |> Seq.iter (fun exe -> submitTopology exe "." uploadedJarLocation nimbus_host nimbus_port |> ignore )

 //packages the jar with topology runtime components
 //uploads the jar to nimbus
 //submits the topology to storm
let runTopology binDir  nimbus_host nimbus_port =  
    let jar = makeJar binDir
    let uploadedJarLocation = withNimbus nimbus_host nimbus_port (uploadJar jar)
    submitTopologies binDir uploadedJarLocation nimbus_host nimbus_port