module Storm.JsonIOTests

open NUnit.Framework
open Swensen.Unquote
open Storm.TestTopology
open Storm.Multilang
open System

[<Test>]
let ``reads handshake``() = 
    let (in',_) = JsonIO.start "test"
    
    new System.IO.StringReader(
            """{"pidDir":"C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                "context":{"task->component":{"1":"AddOneBolt","2":"AddOneBolt","3":"ResultBolt","4":"ResultBolt","5":"SimpleSpout","6":"__acker"},"taskid":5},
                "conf":{"storm.id":"FstSample-2-1456522507","dev.zookeeper.path":"\/tmp\/dev-storm-zookeeper","topology.tick.tuple.freq.secs":30,"topology.classpath":null}}"""
                .Replace("\r\n","")+"\nend\n")
    |> Console.SetIn

    let expected = InCommand<Schema>.Handshake(
                    Conf ["storm.id",box "FstSample-2-1456522507"; "dev.zookeeper.path", box "/tmp/dev-storm-zookeeper"; "topology.tick.tuple.freq.secs", box 30L; "topology.classpath", null],
                    "C:\\Users\\eugene\\storm-local\\workers\\9ee413b6-c7d2-4896-ae4d-d150da988822\\pids",
                    {TaskId=5L;ComponentId="SimpleSpout";Components=Map [1L,"AddOneBolt"; 2L,"AddOneBolt"; 3L,"ResultBolt"; 4L, "ResultBolt"; 5L,"SimpleSpout"; 6L,"__acker"]})
    async {
        return! in'()
    } |> Async.RunSynchronously =! expected


[<Test>]
let ``reads next``() = 
    let (in',_) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"id":"","command":"next"}"""+"\nend\n")
    |> Console.SetIn

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Next


[<Test>]
let ``reads ack``() = 
    let (in',_) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"id":"zzz","command":"ack"}"""+"\nend\n")
    |> Console.SetIn

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Ack "zzz"


[<Test>]
let ``reads nack``() = 
    let (in',_) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"id":"zzz","command":"fail"}"""+"\nend\n")
    |> Console.SetIn

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Nack "zzz"


[<Test>]
let ``reads tuple``() = 
    let (in',_) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[62],"task":1,"stream":"Original","id":"2651792242051038370"}"""+"\nend\n")
    |> Console.SetIn

    async {
        return! in'()
    } |> Async.RunSynchronously =! InCommand<Schema>.Tuple(Original {x=62},"2651792242051038370","AddOneBolt","Original",1L)


[<Test>]
let ``writes tuple``() = 
    let (_,out) = JsonIO.start "test"
    
    let sw = new System.IO.StringWriter()
    sw |> Console.SetOut

    out(Emit(Original {x=62},Some "2651792242051038370",["123"],"Original",None))
    Threading.Thread.Sleep(10)
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[62],"anchors":["123"],"stream":"Original","need_task_ids":false}"""+Environment.NewLine+"end"+Environment.NewLine

[<Test>]
let ``rw complex tuple``() = 
    let (in',out) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[62,"a"],"task":1,"stream":"Even","id":"2651792242051038370"}"""+"\nend\n")
    |> Console.SetIn
    let sw = new System.IO.StringWriter()
    sw |> Console.SetOut
    
    let even = Even({x=62},{str="a"})
    
    out(Emit(even,Some "2651792242051038370",[],"Even",None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(even,"2651792242051038370","AddOneBolt","Even",1L)
    Threading.Thread.Sleep 10
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[62,"a"],"anchors":[],"stream":"Even","need_task_ids":false}"""+Environment.NewLine+"end"+Environment.NewLine


[<Test>]
let ``rw option tuple``() = 
    let (in',out) = JsonIO.start "test"
    
    new System.IO.StringReader("""{"comp":"AddOneBolt","tuple":[{"Case":"Some","Fields":["zzz"]}],"task":1,"stream":"MaybeString","id":"2651792242051038370"}"""+"\nend\n")
    |> Console.SetIn
    let sw = new System.IO.StringWriter()
    sw |> Console.SetOut
    
    let t = MaybeString(Some "zzz")
    
    out(Emit(t,Some "2651792242051038370",[],"MaybeString",None))
    async {
        return! in'()
    } 
    |> Async.RunSynchronously =! InCommand<Schema>.Tuple(t,"2651792242051038370","AddOneBolt","MaybeString",1L)
    sw.ToString() =! """{"command":"emit","id":"2651792242051038370","tuple":[{"Case":"Some","Fields":["zzz"]}],"anchors":[],"stream":"MaybeString","need_task_ids":false}"""+Environment.NewLine+"end"+Environment.NewLine
