# TauMulticastReferenceCSharp
Tautracker multicast client reference C# implementation (with basic CLI)

[![Build Status](https://api.travis-ci.com/wafflecomposite/TauMulticastReferenceCSharp.svg?branch=master)](https://travis-ci.com/github/wafflecomposite/TauMulticastReferenceCSharp/builds)

Minimum requirements is `.Net Framework >=4` or `.Net Core >= 2.1`. No external dependencies.

Core client files to use in plugins is [TauObjects.cs](https://github.com/wafflecomposite/TauMulticastReferenceCSharp/blob/master/TauMulticastReferenceCSharp/TauObjects.cs) and [TauMulticast.cs](https://github.com/wafflecomposite/TauMulticastReferenceCSharp/blob/master/TauMulticastReferenceCSharp/TauMulticast.cs)

Two .sln files here is mostly for Travis, but you can use them to build CLI tool, just keep in mind that you will need to delete the /bin/ and /obj/ folders if you want to switch between framework and core versions (`dotnet restore` may also come in handy for core version)