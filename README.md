# Il2CppTsBindgen

This is a very work in progress and incredibly niche tool that uses `Cpp2IL.Core` to convert a built Unity WebAssembly (+Il2Cpp) game into TypeScript classes. Think [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) but for WASM.
These classes will be designed to call into [UnityWebModkit](https://github.com/nsfury/UnityWebModkit) for any action needed (object creation, reading/writing to fields, calling methods, etc).

Current project status: ~~C# side done for now~~ (I lied), beginning to work on the TypeScript portion which uses the TS compiler API

Very much open to contributions :-)

## current todos
- fix fully shared generic classes using their namespace in their type name? - c#
- gather separated generic class impls into one (or, unique naming if offsets change) - c#
- `Il2Cpp` namespace prepend for type annotations - ts
- recursive nested class resolving - c# & ts
  - and beyond..

## wontfix
- Nested classes don't extend classes they inherit from (typecast is needed to access parent members)