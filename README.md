# Il2CppTsBindgen

This is a very work in progress and incredibly niche tool that uses `Cpp2IL.Core` to convert a built Unity WebAssembly (+Il2Cpp) game into TypeScript classes. 
These classes will be designed to call into [UnityWebModkit](https://github.com/nsfury/UnityWebModkit) for any action needed (object creation, reading/writing to fields, calling methods, etc).

Current project status: C# side done for now, beginning to work on the TypeScript portion which uses the TS compiler API

Very much open to contributions :-)