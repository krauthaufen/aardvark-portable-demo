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

## Packaging notes (consumer gotchas)

- **FSharp.Core**: the portable packages require `>= 10.1.301`, but Fable's
  design-time build injects an implicit `FSharp.Core 10.0.x`, which NuGet
  rejects as a downgrade (NU1605). The fsproj pins it with
  `<PackageReference Update="FSharp.Core" Version="10.1.301" />`
  (`Update`, not `Include` — a second `Include` is a duplicate, NU1504).
- **Fable.AST**: the shader plugin's attributes derive from `Fable.AST`
  types, and `Aardvark.Portable.Shader.Plugin` does not (yet) flow that
  dependency — reference `Fable.AST 5.0.0` directly alongside the plugin.
- **The plugin must be a direct reference** — `dotnet fable` only scans
  direct package references for compiler plugins.
- npm side: `@aardworx/wombat.{base,adaptive,shader,rendering,dom}` versions
  must satisfy the ranges the backend bindings were built against (see
  `package.json`).
