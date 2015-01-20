module FsJson
open System
open System.Text.RegularExpressions
open System.Text
open System.IO
open System.Globalization

type Json = 
    | JsonObject of Map<string,Json>
    | JsonString of String
    | JsonFloat of float
    | JsonInt of int
    | JsonBool of bool
    | JsonNull
    | JsonArray of Json array
    static member (?)(a, p) = 
        match a with
        | JsonObject js ->
            let v = js |> Map.tryFind p
            match v with Some p -> p | None -> JsonNull
        | _ -> JsonNull //failwith (sprintf "json value is not an object: %A" a)
    static member (?<-)(a, k, v) = 
        match a with
        | JsonObject m -> m |> Map.add k v |> JsonObject
        | _ -> failwith  (sprintf "Assignment error: json value is not a JsonObject: %A" a)
    member x.Remove name =
        match x with
        | JsonObject m -> m |> Map.remove name |> JsonObject
        | _ -> failwith  (sprintf "Remove error: json value is not a JsonObject: %A" name)
    member x.Val =
        match x with
        | JsonString s -> s 
        | JsonFloat n -> n.ToString()
        | JsonInt n -> n.ToString()
        | _ -> ""
    member x.ValD =
        match x with
        | JsonString s -> DateTime.Parse(s)
        | JsonFloat n -> DateTime.FromFileTime(Convert.ToInt64(n))
        | JsonInt n -> DateTime.FromFileTime(Convert.ToInt64(n))
        | x -> failwith (sprintf "cannot convert value to date: %A" x)
    member x.ValDF format =
        match x with
        | JsonString s -> DateTime.ParseExact(s, format, CultureInfo.InvariantCulture)
        | x -> failwith (sprintf "cannot convert value to date: %A" x)
    member x.ValF =
        match x with
        | JsonFloat n -> n
        | JsonInt n -> (float)n
        | _ -> failwith "not a numeric value"
    member x.ValB =
        match x with
        | JsonBool n -> n
        | _ -> failwith "not a boolean value"
    member x.ValI =
        match x with
        | JsonInt n -> n
        | JsonFloat n -> Convert.ToInt32(n)
        | _ -> failwith "not a numeric value"
    member x.Item
        with get(index) =
            match x with
            | JsonArray arr -> arr.[index]
            | _ -> failwith "Json object is not an array"
    member x.Named
        with get(index) =
            match x with
            | JsonObject pMap -> pMap.[index]
            | _ -> failwith "Json value is not a JsonObject"
    member x.Count = match x with JsonArray arr -> arr.Length | _ -> 0
    member x.Names =
        match x with
        | JsonObject m -> m |> Map.toList |> List.map (fun (k,v) -> k)
        | x -> []
    member x.HasName k = 
        match x with
        | JsonObject map -> map.ContainsKey k
        | _ -> false
    member x.IsNotNull = match x with JsonNull -> false | _ -> true
    member x.Array = match x with JsonArray arr -> arr | _ -> failwith "json value is not a JsonArray"
    member x.Map = match x with JsonObject j ->  j | _ -> failwith "json value is not a JsonObject"
and JsonSlot = String * Json

type Scanner = Scn of bool*(Char->Scanner)
type Strn = int*string

let tkTrue  = "true".ToCharArray()
let tkFalse = "false".ToCharArray()
let tkNull  = "null".ToCharArray()

let errorOut msg (inp:Strn) = 
    let start,str = inp
    let e =
        if start >= str.Length then "[end of input]"
        elif start + 100 < str.Length then sprintf "%s..." (str.Substring(start,100))
        else str.Substring(start)
    failwith (sprintf "%s at %A" msg e)

let rec (|Star|_|) f acc s =
    match f s with
    | Some(m, rest) -> (|Star|_|) f (m::acc) rest
    | None -> (acc |> List.rev, s) |> Some

let rec (|Star2|_|) f acc s =
    match f s with
    | Some((k,v), rest) -> (|Star2|_|) f (acc |> Map.add k v) rest
    | None -> Some (acc,s)

let (|Token|_|) (pattern:char array) (inp:Strn) =
    let start,str = inp
    let rec loop i j = 
        if j >= pattern.Length then Some(i,str)
        elif i >= str.Length  then None
        else
            let c = str.[i]
            if Char.IsWhiteSpace(c) then loop (i + 1) j
            elif c = pattern.[j] then loop (i + 1) (j + 1)
            else None
    loop start 0

let (|CToken|_|) c (inp:Strn) = (|Token|_|) [|c|] inp

let inline append (sb:StringBuilder) (c:char) = sb.Append(c)   |> ignore
let inline appendS (sb:StringBuilder) (s:string) = sb.Append(s) |> ignore
let inline newSb() = new StringBuilder()

let (|PJsonString|_|) (start:int,str:string) =
    let rec skipWS i  =
        let nextI = i + 1
        let c = str.[i]
        if Char.IsWhiteSpace c then skipWS nextI
        elif c = '"' then scanPlain nextI nextI
        else None
    and scanPlain st curr =
        let nextI = curr + 1
        let c = str.[curr]
        match c with
        | '"'  -> Some(str.Substring(st,curr-st),(nextI,str))
        | '\\' -> let sb = newSb() in appendS sb (str.Substring(st,curr-st)); scanEsc sb nextI
        | _    -> scanPlain st nextI
    and scanReg sb i =
        let nextI = i + 1
        let c = str.[i]
        match c with
        | '"' -> Some(sb.ToString(),(nextI,str))
        | '\\' -> scanEsc sb (i+1)
        | c -> append sb c; scanReg sb (i+1)
    and scanEsc sb i =
        let nextI = i + 1
        let c = str.[i]
        match c with
        | '\\' | '/'| '"'-> append sb c; scanReg sb nextI
        | 'b' -> scanReg sb nextI
        | 'f' -> append sb '\f'; scanReg sb nextI
        | 'n' -> append sb '\n'; scanReg sb nextI
        | 'r' -> append sb '\r'; scanReg sb nextI
        | 't' -> append sb '\t'; scanReg sb nextI
        | 'u' -> scanUnicode sb nextI
        | _ -> failwith (sprintf "not a valid escaped character %A" c)
    and scanUnicode sb i =
        let encoded = str.Substring(i,3)
        let n = (char) (Int32.Parse(encoded, Globalization.NumberStyles.HexNumber))
        append sb n
        scanReg sb (i+1)
    skipWS start
        
let (|PJsonNumber|_|) (inp:Strn) =
    let startIndex,str = inp
    let len = str.Length
    let check i f = if i >= len then (i-1) else f i
    let rec ws i = if Char.IsWhiteSpace(str.[i]) then check (i+1) ws else numstart i
    and numstart i = 
        let c = str.[i]
        match c with
        | '-' -> digit (i+1)
        | x when Char.IsDigit(x) -> check (i+1) digit
        | _ -> -1
    and digit i =
        let c = str.[i]
        match c with
        | ',' | '}' | ']' -> (i-1)
        | '.' -> digit (i+1)
        | 'e' | 'E' -> check  (i+1) exponentStart
        | x when Char.IsDigit(x) -> check (i+1) digit
        | x when Char.IsWhiteSpace(x) -> (i-1)
        | _ -> -1
    and exponentStart i = 
        let c = str.[i]
        match c with
        | '-' | '+' ->  check (i+1) exponentStart2
        | x when Char.IsDigit(x) -> check (i+1) exponent
        | _ -> -1
    and exponentStart2 i = 
        let c = str.[i]
        match c with
        | x when Char.IsDigit(x) -> check (i+1) exponent
        | _ -> -1
    and exponent i = 
        let c = str.[i]
        match c with
        | ',' | '}' | ']' -> (i-1)
        | x when Char.IsDigit(x) -> check (i + 1) exponent
        | x when Char.IsWhiteSpace(x) -> (i - 1)
        | _ -> -1
    let endIndex = check startIndex ws
    if endIndex >= startIndex then
        let jNum:Json option =
            let numStr = str.Substring(startIndex, endIndex - startIndex + 1)
            if numStr.IndexOfAny([|'.';'e';'E'|]) > 0 then
                let suc,num = Double.TryParse(numStr,NumberStyles.AllowExponent ||| NumberStyles.Float,Globalization.CultureInfo.CurrentCulture) 
                if suc then Some(JsonFloat num) else None
            else
                let suc,num = Int32.TryParse(numStr, NumberStyles.Integer, Globalization.CultureInfo.CurrentCulture)
                if suc then Some(JsonInt num)
                else
                    let suc,num = Double.TryParse(numStr, NumberStyles.Number, Globalization.CultureInfo.CurrentCulture)
                    if suc then Some(JsonFloat num) else None
        match jNum with Some n -> Some(n,(endIndex+1,str)) | _ -> None
    else None
    //regex: @"-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?\b"

let (|TkTrue|_|) inp = match inp with Token tkTrue (rest) -> Some(true,rest) | _ -> None
let (|TkFalse|_|) inp = match inp with Token tkFalse (rest) -> Some(false,rest) | _ -> None

let rec (|PJsonValue|_|) (inp:Strn) =
    match inp with
    | PJsonString (v, rest)                              -> Some (v |> JsonString, rest)
    | PJsonNumber (n, rest)                              -> Some (n, rest)
    | TkTrue (b, rest)                                   -> Some (JsonBool(b), rest)
    | TkFalse (b, rest)                                  -> Some (JsonBool(b), rest)
    | Token tkNull (rest)                                -> Some (JsonNull,rest)
    | CToken '{' (PJsonObject(slots, CToken '}' (rest))) -> Some (JsonObject(slots), rest)
    | CToken '[' (PJsonArray(values, CToken ']' (rest))) -> Some (JsonArray(values), rest)
    //we could have returned None for the default case here
    //but the following help us to locate errors better with incorrectly formatted json
    | CToken ',' (_,_) -> None 
    | CToken ']' (_,_) -> None
    | CToken '}' (_,_) -> None
    | _ -> errorOut "unable to parse json value" inp
and (|PJsonArray|) inp =
    match inp with
    | Star (|PPArrayValue|_|) [] (values, rest)  -> (List.toArray values, rest)
    | _ -> errorOut "unable to parse array" inp
and (|PJsonObject|) inp =
    match inp with
    | Star2 (|PJsonSlot|_|) Map.empty (slots, rest) -> (slots, rest)
    | _ -> errorOut "unable to parse object" inp
and (|PJsonSlot|_|) inp =
    match inp with
    | PJsonString(n, CToken ':' (PPSlotValue(v,rest))) -> Some ((n,v),rest)
    |_ -> None
and (|PPArrayValue|_|) inp =
    match inp with
    | PJsonValue (v, rest) ->
        match rest with
        | CToken ',' (rest2)    -> Some (v,rest2) 
        | CToken ']' (_)        -> Some (v,rest)
        | _                     -> errorOut "Expecting a ',' or ']'" rest
    | _ -> None
and (|PPSlotValue|_|) inp =
    match inp with
    | PJsonValue (v, rest) ->
        match rest with
        | CToken ',' (rest2) -> Some (v,rest2)
        | CToken '}' (_)     -> Some (v,rest) 
        | _                  -> errorOut "Expecting a ',' or '}'" rest
    | _ -> None

let parse (s:String) =
    match (0,s) with
    | PJsonValue (json,_) -> json
    | _ -> failwith "unable to parse"

///performs a pre-order traversal and evaulates the given function for each Json node
///collects the results in a sequence if 'Some' value is returned by the function
///the given function accepts a string (name) and json (value); name is empty for the root object
let choose (f:String*Json->'a option) (j:Json)  =
    let rec preorder (n,j) =
        seq{
            yield f (n,j)
            match j with
            | JsonObject pMap -> for kv in pMap do yield! preorder (kv.Key,kv.Value)
            | JsonArray arr -> for i in 0..arr.Length-1 do yield! preorder (n,arr.[i])
            | _ -> ()}
    preorder ("",j) |> Seq.choose(fun x -> x)

let serialize (json:Json) =
    let sb = StringBuilder()
    let (!>) (s:string) = sb.Append(s) |> ignore
    let appS (s:string) = 
        !>"\""
        for i in 0..s.Length-1 do 
            let c = s.[i]
            match c with
            | '"'  -> !> "\\\""
            | '\\' -> !> "\\\\"
            | '\b' -> !>"\\b"
//            | '/'  -> !>"\\/"
            | '\f' -> !>"\\f"
            | '\n' -> !>"\\n"
            | '\r' -> !>"\\r"
            | '\t' -> !>"\\t"
            | x    -> sb.Append(x) |> ignore
        !>"\""
    let rec loop j =
        match j with
        | JsonObject pMap -> 
            !>"{ "
            if not(Map.isEmpty pMap) then
                pMap |> Map.iter (fun k v -> appS k; !>" : ";  loop v; !>", " )
                sb.Length <- sb.Length - 2
            !>" }"            
        | JsonString str -> appS str
        | JsonFloat n    -> !> n.ToString()
        | JsonInt n      -> !> n.ToString()
        | JsonBool b     -> if b then !>"true" else !>"false"
        | JsonNull       -> !>"null"
        | JsonArray arr  ->
            !>"[ "
            if not(Array.isEmpty arr) then
                arr |> Array.iter (fun j -> loop j; !>", ")
                sb.Length <- sb.Length - 2 //remove the last comma
            !>" ]"
    loop json
    sb.ToString()

let rec jval (o:obj) =
    match o with
    | :? string as s        -> JsonString s
    | :? int as i           -> JsonInt i
    | :? float as f         -> JsonFloat f
    | :? DateTime as d      -> JsonString (d.ToString("s"))
    | :? bool as b          -> JsonBool b
    | :? Json as j          -> j
    | :? unit               -> JsonNull
    | :? (int seq) as os    -> JsonArray [| for j in os -> JsonInt j|]
    | :? (string seq) as os -> JsonArray [| for j in os -> JsonString j|]
    | :? (Json seq) as os   -> JsonArray [| for j in os -> j|]
    | :? (float seq) as os  -> JsonArray [| for j in os -> JsonFloat j|]
    | :? (string*obj) as d  -> jval [d] 
    //convert 2-tuple with the value a seq, list, etc. to JsonObject
    | :? (string*string list) as d  -> jval [d] 
    | :? (string*string array) as d -> jval [d] 
    | :? (string*string seq) as d   -> jval [d] 
    | :? (string*int list) as d     -> jval [d] 
    | :? (string*int array) as d    -> jval [d] 
    | :? (string*int seq) as d      -> jval [d] 
    | :? (string*float list) as d   -> jval [d] 
    | :? (string*float array) as d  -> jval [d] 
    | :? (string*float seq) as d    -> jval [d] 
    | :? (string*Json list) as d    -> jval [d] 
    | :? (string*Json array) as d   -> jval [d] 
    | :? (string*Json seq) as d     -> jval [d] 
    //convert sequence of 2-tuples to JsonObject
    | :? ((string*int) seq) as d    -> d |> Seq.map (fun (k,v) -> k, JsonInt v) |> Map.ofSeq |> JsonObject
    | :? ((string*float) seq) as d -> d |> Seq.map (fun (k,v) -> k,JsonFloat v) |> Map.ofSeq |> JsonObject
    | :? ((string*string) seq) as d -> d |> Seq.map (fun (k,v) -> k,JsonString v) |> Map.ofSeq |> JsonObject
    | :? ((string*Json) seq) as d -> d |> Map.ofSeq |> JsonObject
    | :? ((string*obj) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    //object with seq value
    | :? ((string*int seq) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*string seq) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*float seq) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*Json seq) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    //object with list value
    | :? ((string*int list) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*string list) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*float list) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*Json list) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    //object with array value
    | :? ((string*int array) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*string array) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*float array) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? ((string*Json array) seq) as d -> d |> Seq.map (fun (k,v) -> k,jval v) |> Map.ofSeq |> JsonObject
    | :? (obj seq) as os    -> JsonArray [| for j in os -> jval j|]
    | o -> failwithf "Cannot convert value %A to JSON - consider using an explict constructor such as JsonString, etc." o
