# Aardvark.Portable demo (Fable / WebGPU)

A standalone consumer of the published **Aardvark.Portable** NuGet packages —
no project references, no local feeds. A spinning cube with a custom
FShade-syntax shader (`[<ShaderEffect>]`, translated to wombat IR by the
Fable plugin at compile time), an orbit camera, and an adaptive DOM overlay
(tint + spin-speed controls), rendered with WebGPU via the wombat stack.

```
App.fs        the whole app: shader, scene, adaptive state, DOM overlay
Demo.fsproj   NuGet references (see notes below)
package.json  the @aardworx/wombat.* npm packages + vite
index.html    mounts #app, loads build/App.fs.js
```

## Run

```bash
dotnet tool restore     # fable 5.0.0
npm install
npm run dev             # fable compile + vite dev server
```

Requires a WebGPU-capable browser (secure context — localhost counts).

## Packaging notes

- Three package references is all it takes (the web shell pulls the rest
  transitively). The early `prerelease0001` packages needed an FSharp.Core
  `Update`-pin and a direct `Fable.AST` reference — both fixed since
  `prerelease0002/0003` (relaxed + floor-built FSharp.Core dependency; the
  plugin flows its own Fable.AST).
- **The plugin must be a direct reference** — `dotnet fable` only scans
  direct package references for compiler plugins.
- npm side: `@aardworx/wombat.{base,adaptive,shader,rendering,dom}` versions
  must satisfy the ranges the backend bindings were built against (see
  `package.json`).
- **Target `netstandard2.0`** (since `prerelease0010`). The packages
  multi-target net10.0 (the .NET arm) and netstandard2.0 (the Fable arm
  as a real assembly); the nuget dependency groups put Aardvark.*,
  FShade.* and FSharp.Data.Adaptive under net10.0 only. A
  netstandard2.0 web project therefore restores none of the .NET
  rendering stack, `dotnet fable` compiles only the portable sources
  (the `FSharp.Data.Adaptive` namespace comes from
  `Aardvark.Portable.Adaptive`'s shim over `@aardworx/wombat.adaptive`),
  and the IDE reads real symbols from the netstandard2.0 dlls.
  `OutputType Exe` + `[<EntryPoint>]` still work — `dotnet build`
  produces a plain dll and Fable emits the entry-point call into the
  generated JS; only `dotnet run` refuses (the web app is run by vite,
  never by dotnet).
