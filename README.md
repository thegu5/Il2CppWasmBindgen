# Il2CppWasmBindgen

This is a very work in progress and incredibly niche tool that uses `Cpp2IL.Core` to convert a built Unity Web build into assemblies for use in browser contexts. Think [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) but for WASM.
This will mean that a plugin project can import the generated dlls and compile itself to WASM via NativeAOT-LLVM, and be ran via a userscript.

Current project status: Creating a basic POC

Very much open to contributions :-)

### things
- Wait [this pr](https://github.com/dotnet/runtimelab/pull/2605) so the downloaded package from [spin](https://github.com/dicej/spin-dotnet-sdk/releases/tag/canary) can be removed
- Wait for Il2CppInterop to [adopt AsmResolver 6](https://github.com/BepInEx/Il2CppInterop/pull/124/) so hopefully [AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer/pull/17) can too (removing the manually downloaded package)

... And beyond

