FsShelter [![Windows build](TBD)](TBD) [![Mono/OSX build](https://travis-ci.org/Prolucid/FsShelter.svg?branch=master)](https://travis-ci.org/Prolucid/FsShelter)
=======

A library for defining and running Apache Storm topologies in F# using statically typed streams.

Based on and a complete rewrite of [FsStorm](https://github.com/FsStorm) with the goals of static typing, modularity and pluggable serialization.
Comes bundled with Json serialization, Thrift and Protobuf (Thrift and Protobuf require corresponding Storm implementations [Thriftshell](https://github.com/prolucid/thriftshell) or [Protoshell](https://github.com/prolucid/protoshell)). 

See [docs][docs] for for an intro and an overview.

Join the conversation: [![Gitter](https://badges.gitter.im/Join%20Chat.svg)](TBD)

# Limitations
* At the moment FsShelter doesn't support direct emits.
* [STORM-1644](https://issues.apache.org/jira/browse/STORM-1644): Currently, when running on Windows, you'll need to add the bin output to the path prior to starting Storm.

# Building
On Windows, from the cloned root:
```
build
```
or on Linux/OSX:
```
./build.sh
```

# Running the tests
Building from command line runs the unit tests.

IDE: Install NUnit plugin for VS or MonoDevelop to see the unit-tests in Test Explorer and step through the code under debugger.

# Submitting the topology
Have a local [Storm](https://storm.apache.org/downloads.html) installed and running.
```
samples\Simple\bin\Release\Simple submit
```
or, if running on Mono:
```
mono samples/Simple/bin/Release/Simple.exe submit
```

# Seeing the topology in action
Open [Storm UI](http://localhost:8080/) and see the Storm worker logs for runtime details.

# License
FsShelter is Apache 2.0 licensed and free to use and modify.

# Commercial support
Contact [Prolucid](https://prolucid.ca) for commercial support.

[docs]:https://prolucid.github.io/FsShelter/
