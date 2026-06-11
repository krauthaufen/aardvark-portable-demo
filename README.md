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
- **Pin `FSharp.Data.Adaptive` with `ExcludeAssets="all"`.** The portable
  packages declare it for their .NET dlls; on Fable the
  `FSharp.Data.Adaptive` namespace is provided by
  `Aardvark.Portable.Adaptive`'s shim over `@aardworx/wombat.adaptive`.
  Without the exclusion, `dotnet fable` downloads and compiles the real
  FSharp.Data.Adaptive sources into `fable_modules` — slow, and a second
  (unused) adaptive system in the compilation.
