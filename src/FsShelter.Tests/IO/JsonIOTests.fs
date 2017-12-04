module FsShelter.JsonIOTests

open NUnit.Framework
open Swensen.Unquote
open FsShelter.TestTopology
open FsShelter.Multilang
open System
open JsonIO
open CommonTests

[<Test>]
let ``reads handshake``() = 
    let sr = new System.IO.StringReader(
                    """{"pidDir":"C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                        "context":{"task->component":{"1":"AddOneBolt","2":"AddOneBolt","3":"ResultBolt","4":"ResultBolt","5":"SimpleSpout","6":"__acker"},"taskid":5},
                        "conf":{"FsShelter.id":"Simple-2-1456522507","dev.zookeeper.path":"\/tmp\/dev-storm-zookeeper","topology.tick.tuple.freq.secs":30,"topology.classpath":null}}"""
                        .Replace("\r","").Replace("\n","")+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    let expected = InCommand<Schema>.Handshake(
                    Conf ["FsShelter.id",box "Simple-2-1456522507"; "dev.zookeeper.path", box "/tmp/dev-storm-zookeeper"; "topology.tick.tuple.freq.secs", box 30L; "topology.classpath", null],
                    "C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                    {TaskId=5;ComponentId="SimpleSpout";Components=Map [1,"AddOneBolt"; 2,"AddOneBolt"; 3,"ResultBolt"; 4, "ResultBolt"; 5,"SimpleSpout"; 6,"__acker"]})
    in'() |> Async.RunSynchronously =! expected


[<Test>]
let ``reads next``() = 
    let sr = new System.IO.StringReader("""{"id":"","command":"next"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let sr = new System.IO.StringReader("""{"id":"zzz","command":"ack"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let sr = new System.IO.StringReader("""{"id":"zzz","command":"fail"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Nack "zzz"

[<Test>]
let ``reads activate``() = 
    let sr = new System.IO.StringReader("""{"id":"","command":"activate"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
   
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Activate

[<Test>]
let ``reads deactivate``() = 
    let sr = new System.IO.StringReader("""{"id":"","command":"deactivate"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Deactivate

[<Test>]
let ``reads tuple``() = 
    let sr = new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[62],"task":1,"stream":"Original","id":"2651792242051038370"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',_) = JsonIO.startWith (sr,sw) syncOut ignore
    
    in'() |> Async.RunSynchronously =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1)


[<Test>]
let ``writes tuple``() = 
    let sr = new System.IO.StringReader("")
    let sw = new System.IO.StringWriter()
    let (_,out) = JsonIO.startWith (sr,sw) syncOut ignore
    
    out(Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None,None))
    Threading.Thread.Sleep(10)
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[62],"anchors":["123"],"stream":"Original","need_task_ids":false}"""+END

[<Test>]
let ``rw complex tuple``() = 
    let sr = new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[62,"a"],"task":1,"stream":"Even","id":"2651792242051038370"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',out) = JsonIO.startWith (sr,sw) syncOut ignore
    let even = Even({x=62},{str="a"})
    
    async {
        out(Emit(even,Some "2651792242051038370",[],"Even",None,None))
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(even,"2651792242051038370","AddOneBolt","Even",1)
    Threading.Thread.Sleep 100
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[62,"a"],"stream":"Even","need_task_ids":false}"""+END


[<Test>]
let ``rw option tuple``() = 
    let sr = new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[{"Case":"Some","Fields":["zzz"]}],"task":1,"stream":"MaybeString","id":"2651792242051038370"}"""+END)
    let sw = new System.IO.StringWriter()
    let (in',out) = JsonIO.startWith (sr,sw) syncOut ignore
    let t = MaybeString(Some "zzz")
    
    async {
        out(Emit(t,Some "2651792242051038370",[],"MaybeString",None,None))
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","MaybeString",1)
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[{"Case":"Some","Fields":["zzz"]}],"stream":"MaybeString","need_task_ids":false}"""+END


[<Test>]
[<Category("performance")>]
let ``roundtrip throughput``() =
    let count = 10000 
    use mem = new IO.MemoryStream()
    let (in',out') = JsonIO.startWith (new IO.StreamReader(mem), new IO.StreamWriter(mem)) syncOut ignore

    let sw = System.Diagnostics.Stopwatch.StartNew()
    async {
        for i in {1..count} do
            Emit(justFields,Some "2651792242051038370",[],"JustFields",Some 1,None) |> out'

        mem.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        for i in {1..count} do
            do! in'() |> Async.Ignore
    } |> Async.RunSynchronously
    sw.Stop()
    printf "[Json] Ellapsed: %dms, %f/s\n" sw.ElapsedMilliseconds ((float count)/sw.Elapsed.TotalSeconds)
