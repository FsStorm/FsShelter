FsShelter [![Windows Build](https://ci.appveyor.com/api/projects/status/c0oom3oyr8qnrsc8?svg=true)](https://ci.appveyor.com/project/et1975/fsshelter) [![Mono/OSX build](https://travis-ci.org/Prolucid/FsShelter.svg?branch=master)](https://travis-ci.org/Prolucid/FsShelter) [![NuGet version](https://badge.fury.io/nu/fsshelter.svg)](https://badge.fury.io/nu/fsshelter)
=======

FsShelter is a library for defining and running Apache Storm topologies in F# using statically typed streams.

It is a complete rewrite of [FsStorm](https://github.com/FsStorm) with the goals of static typing, modularity, and pluggable serialization.
It comes bundled with Json serialization and Protobuf (Protobuf requires corresponding Storm multilang serializer implementation  [Protoshell](https://github.com/prolucid/protoshell)). 

See [docs][docs] for for an intro and an overview.

Join the conversation: [![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/Prolucid/FsShelter)

## Limitations
* At the moment FsShelter doesn't support direct emits.
* [STORM-1644](https://issues.apache.org/jira/browse/STORM-1644): Currently, when running on Windows, the process will run under cmd.exe incurring slight overhead.

# Building
On Windows, from the cloned root:
```
build
```
or on Linux/OSX:
```
./build.sh
```

## Running the tests
Building from the command line runs the unit tests.

IDE: Install the NUnit plugin for VS or MonoDevelop to see the unit tests in Test Explorer and step through the code with the debugger.

## Submitting the topology
Have a local [Storm](https://storm.apache.org/releases.html) instance installed and running.
```
samples\WordCount\bin\Release\WordCount submit-local
```
or, if running on Mono:
```
mono samples/WordCount/bin/Release/WordCount.exe submit-local
```

## Seeing the topology in action
Open [Storm UI](http://localhost:8080/) and see the Storm worker logs for runtime details.

## License
FsShelter is Apache 2.0 licensed and is free to use and modify.

## Commercial support
Contact [Prolucid](http://prolucid.ca) for commercial support.

[docs]:https://prolucid.github.io/FsShelter/
