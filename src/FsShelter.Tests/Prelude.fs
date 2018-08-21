namespace FsShelter

module TraceLog = 
    open System

    let file = new System.IO.StreamWriter("trace.log")
    let formatLine fmt ([<ParamArray>]args:obj[]) =
        file.WriteLine(fmt, args)
    let write (str:string) =
        file.WriteLine str
        
    let flush () =
        file.Flush()
