## More about topology schema

FsShelter will serialize most data types you might want to use in the definition of the DU case for the topology streams. There are however several things to keep in mind...

## Grouping expressions

FsShelter DSL uses F# lambdas to define grouping expressions. Since this has to be transformed into a list of fields Storm can understand, only a simple projection of properties is supported, so don't expect to be able to call any functions.

Examples of structural nesting supported:

```fsharp
type Number<'t> = { X:'t; Desc:string }
type ComplexNumber = { Re:float; Im:float }

type RecordSchema = 
    | Original of int
    | Described of Number<int>
    | Translated of Number<int> option
    | Complex of Number<ComplexNumber>
```

Here is how FsShelter will present the schema to Storm when declaring stream/outputs:

* `Original`: [Item]()

* `Described`: [Item.X, Item.Desc]()

* `Translated`: [Item]()

* `Complex`: [Item.X, Item.Desc]()

And when referenced in the grouping expressions, FsShelter will translate the fields back to these names:

```fsharp
open FsShelter.DSL
#nowarn "25" // for stream matching expressions
// group tuples on Original stream, the `int` value is directly accessible
let ex1 = Group.by (function Original x -> x)
// group tuples on Described stream, flattened to [X] for Storm
let ex2 = Group.by (function Described x -> x.X) 
// group tuples on Translated stream, not flattened - the `X` and `Desc` fields will not be accessible to Storm
let ex3 = Group.by (function Translated x -> x)
// group tuples on Complex stream, flattened to [X] for Storm, `X.Re` is not accessible
let ex4 = Group.by (function Complex x -> x.X)
```

As you can see this flattening of records is done only one layer deep. Also keep in mind that while use of collection types for fields is possible it doesn't provde any meaningful way to express that to Storm.
In summary:

* there is no attempt to flatten types other than records

* fields not presented to Storm in the output declaration are transmitted as part of opaque (parent's type) blobs

* there's no way to address a value wrapped in an Option (or any other DU), only entire Option field can be used in a grouping

## Implementation details and performance considerations

FsShelter will serialize most schemata exactly as you would imagine, but there are always "interesting" scenarios.
Here are some of the implementation details to keep in mind (the serializers below are provided in the `FsShelter.Multilang` package):

* Json serialization is implemented with Newtonsoft.Json, F# types are mostly serializable, but might look odd on the wire (Option is a DU, think about that for a second).

* Binary serializer (Protobuf) uses [FsPickler](https://github.com/nessos/FsPickler) and you might be able to optimize the payload via its APIs accordingly.

* Both serializers support different set of native representations, consequently performance and interopability with non-F# components will vary.

* Both will try to map the field values to Java types before passing the tuple on to Storm and will try to convert back before passing it to an F# component.

## System and other arbitrary streams names

Certain features of Storm are available through special streams, like "__tick". Since DU case name like that would look very wrong in an F# application, FsShelter checks for `DisplayName` attribute that facilitates the mapping, for example:

```fsharp
type TimedSchema = 
    | [<System.ComponentModel.DisplayName("__tick")>] Tick
```

Will use `__tick` as the name for the stream, while keeping F# conventions in place for the DU case.
