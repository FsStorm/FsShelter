#### 2.1.0 - Aug 2018
* rewriten spouts fix idle deadlocks

#### 2.0.1 - Aug 2018
* colorized graphs

#### 2.0.0 - Aug 2018
* DSL modules for Spouts and Bolts
* sync dispatch and disruptor-based self-hosting

#### 1.0.2 - Apr 2018
* bugfixes in nested streams support

#### 1.0.1 - Apr 2018
* `bolt1<'a> --> bolt2 |> Shuffle.on Stream` syntax support

#### 1.0.0 - Mar 2018
* support for nested and generic streams
* netstandard 2.0 release

#### 1.0.0-beta-3 - Nov 2017
* netstandard 2.0 release

#### 0.2.9 - Nov 2017
* Fixing spout timeout when running as a shelled component

#### 0.2.7 - Nov 2017
* Reverting the debug logging in Hosting modules

#### 0.2.6 - Nov 2017
* Re-introducing async Next handling in the spout

#### 0.2.5 - Nov 2017
* Make unanchored emit available to bolt's Activate/Deactive

#### 0.2.4 - Nov 2017
* Reverting spout to non-async, adding Activate/Deactive support for bolts

#### 0.2.3 - Oct 2017
* Conditional spout inputs logging

#### 0.2.2 - July 2017
* Concurrent spout Next, self-host optimizations

#### 0.1.4 - July 2017
* Self-hosting: seed the id generator

#### 0.1.3 - July 2017
* Self-hosting tracks split streams correctly

#### 0.1.2 - July 2017
* Self-hosting bugfixes

#### 0.1.0 - Jun 2017
* Added experimenal self-hosting, breaking changes in Management API

#### 0.0.11 - Apr 2017
* Added support for `activate` and `deactivate` messages introduced in Storm 1.1.0

#### 0.0.10 - Jan 2017
* Extending DSL for components defined outside of `topology` CE
* and adding the ability to combine (++) multiple topologies

#### 0.0.9 - Jan 2017
* Adding support for streaming of DU fields

#### 0.0.8 - July 2016
* Adding auto-nacking bolt and corresponding "terminator" component

#### 0.0.7 - May 2016
* Rebuilt with Google.Protobuf beta3
* Removed thrift multilang IO

#### 0.0.6 - Apr 2016
* Added support for system streams (w/o explicit edges)

#### 0.0.5 - Apr 2016
* Added support for Nullable fields

#### 0.0.4 - Apr 2016
* Nuget dependencies, really fixed

#### 0.0.3 - Apr 2016
* Nuget dependencies fixed

#### 0.0.2 - Apr 2016
* Fanout fixed

#### 0.0.1 - Apr 2016
* Out of beta

#### 0.0.1-beta3 - Apr 2016
* Stream name override via DisplayName attr

#### 0.0.1-beta2 - March 2016
* Storm-side serializer bundling added

#### 0.0.1-beta - March 2016
* Initial release
