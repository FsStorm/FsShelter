module Storm.Runtime

open System.Threading
open System.IO


/// test if running on mono runtime
let isMono() = System.Type.GetType("Mono.Runtime") <> null

let mutable private _currentId = 0L
///generate tuple ids (works for single instance spouts, only)

let nextId() = Interlocked.Increment(&_currentId) 
/// diagnostics pid shortcut
let internal pid() = System.Diagnostics.Process.GetCurrentProcess().Id

///creates an empty file with current pid as the file name
let internal createPid pidDir =
    let pid = pid().ToString()
    let path = pidDir + "/" + pid
    use fs = File.CreateText(path)
    fs.Close()
