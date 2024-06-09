# Il2CppWasmBindgen

This is a very work in progress and incredibly niche tool that uses `Cpp2IL.Core` to convert a built Unity WebAssembly (+Il2Cpp) game into assemblies for use in browser contexts. Think [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) but for WASM.
These classes will probably be designed to call into [UnityWebModkit](https://github.com/nsfury/UnityWebModkit) for any action needed (object creation, reading/writing to fields, calling methods, etc).

Current project status: Cleaning up generated assemblies so that they're valid

Very much open to contributions :-)

## current todos
- Fix publiizer screwing up cpp2il-generated attribute types
- Research `InteropServices.Javascript`
- MVP with method calling
- Project template

... And beyond

### random ideas potentially for the future
- have mods be compiled with standalone il2cpp so that their memory layout is the same (?)
  - shared memory between the two wasm modules
