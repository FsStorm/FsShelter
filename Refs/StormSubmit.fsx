#r "StormThrift.dll"
#r "Thrift.dll"
open System.IO
open StormThrift
open System

let default_nimbus_port = 6627

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

//package the appropriate files from the 'binDir'
//into a jar
let makeJar binDir =
    let jarDir = Path.Combine(binDir,"resources")
    printfn "%A" jarDir
    if Directory.Exists(jarDir) |> not then Directory.CreateDirectory(jarDir) |> ignore
    Directory.GetFiles(jarDir) |> Seq.iter (File.Delete)
    Directory.GetFiles (binDir)
    |> Seq.filter (fun f -> let e = Path.GetExtension(f) in e = ".exe" || e=".dll" || e=".config" || e=".sh")
    |> Seq.map (fun f -> f, jarDir + "/" + (Path.GetFileName(f)))
    |> Seq.iter File.Copy
    let jarFn = binDir + "/st.jar"
    if File.Exists jarFn then File.Delete jarFn
    let r = shell "jar" "cf st.jar resources/" (Some binDir) //jar is in java bin dir
    //let r = shell "7z" ("a st.jar " + jarDir)      // 7-zip commmand line
    if r then
        jarFn
    else
        failwith "unable to make jar"

//upload the packaged jar to nimbus (a storm service)
//using thrift protocol
let uploadJar jarFile nimbus_host nimbus_port =
    use tx = new Thrift.Transport.TSocket(nimbus_host,nimbus_port)
    use txf = new Thrift.Transport.TFramedTransport(tx)
    txf.Open()
    try
        use tp = new Thrift.Protocol.TBinaryProtocol(txf)
        use c = new Nimbus.Client(tp)
        let file = c.beginFileUpload()
        use inStr = File.OpenRead(jarFile)
        let chunkSz = 307200
        let buffer = Array.zeroCreate(chunkSz)
        let mutable read = inStr.Read(buffer,0,buffer.Length)
        while read > 0 do
            c.uploadChunk(file,buffer.[0..read-1])
            printfn "uploaded %d bytes" read
            read <- inStr.Read(buffer,0,buffer.Length)
        c.finishFileUpload(file)
        file
    finally
        txf.Close()

//check if running on mono
let isMono() = Type.GetType("Mono.Runtime") <> null

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
    |> Seq.iter (fun exe -> submitTopology exe binDir uploadedJarLocation nimbus_host nimbus_port |> ignore )

 //packages the jar with topology runtime components
 //uploads the jar to nimbus
 //submits the topology to storm
let runTopology binDir  nimbus_host nimbus_port =  
    let jar = makeJar binDir
    let uploadedJarLocation = uploadJar jar nimbus_host nimbus_port
    submitTopologies binDir uploadedJarLocation nimbus_host nimbus_port