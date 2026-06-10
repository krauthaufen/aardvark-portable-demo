module Demo.App

open FSharp.Data.Adaptive
open Aardvark.Portable
open Aardvark.Portable.Render
open Aardvark.Portable.Shader
open Aardvark.Portable.Dom
open Aardvark.Portable.Host
open Aardvark.Portable.Shell.Web

// ─────────────────────────────────────────────────────────────────────
// Aardvark.Portable demo (Fable / WebGPU).
//
// Everything below comes from the published NuGet packages — no project
// references. The shader is authored once in FShade syntax and the
// Aardvark.Portable.Shader.Plugin translates it to wombat IR at Fable
// compile time; the scene/camera/DOM run on wombat.dom + wombat.rendering
// through the portable surface.
// ─────────────────────────────────────────────────────────────────────

// ─── Custom shader: procedural stripes, lambert + rim, adaptive Tint ──

type VertexInput =
    { [<Position>]                            Positions : V4f
      [<Semantic("Normals")>]                 Normal    : V3f
      [<Semantic("DiffuseColorCoordinates")>] TexCoord  : V2f }

type VertexOutput =
    { [<Position>]                            ClipPos  : V4f
      [<Semantic("Normals")>]                 Normal   : V3f
      [<Semantic("DiffuseColorCoordinates")>] TexCoord : V2f }

[<ShaderEffect>]
let stripeVertex (v: VertexInput) =
    vertex {
        return { ClipPos  = (uniform?ModelViewProjTrafo : M44f) * v.Positions
                 Normal   = Vec.normalize v.Normal
                 TexCoord = v.TexCoord }
    }

type FragmentInput =
    { [<Semantic("Normals")>]                 Normal   : V3f
      [<Semantic("DiffuseColorCoordinates")>] TexCoord : V2f }

type FragmentOutput =
    { [<Color>] Color : V4f }

[<ShaderEffect>]
let stripeFragment (f: FragmentInput) =
    fragment {
        // Procedural diagonal stripes from the texcoords.
        let bands  = 8.0f
        let t      = (f.TexCoord.X + f.TexCoord.Y) * bands
        let parity = t - 2.0f * floor (t * 0.5f)
        let stripe = if parity < 1.0f then 0.45f else 1.0f

        let n        = Vec.normalize f.Normal
        let lightDir = Vec.normalize (V3f (0.5f, 1.0f, 0.4f))
        let lambert  = max 0.2f (Vec.dot n lightDir)
        let rim      = 1.0f - abs (Vec.dot n (V3f (0.0f, 0.0f, 1.0f)))
        let glow     = 0.2f * rim * rim

        let tint : V4f = uniform?Tint
        return { Color = V4f (tint.X * stripe * lambert + glow,
                              tint.Y * stripe * lambert + glow,
                              tint.Z * stripe * lambert + glow,
                              1.0f) }
    }

let stripeEffect : Effect =
    Effect.compose [ Effect.ofFunction stripeVertex
                     Effect.ofFunction stripeFragment ]

// ─── Adaptive app state ───────────────────────────────────────────────

let private tints =
    [ "amber", V4d (1.0, 0.8, 0.35, 1.0)
      "teal",  V4d (0.35, 0.9, 0.8, 1.0)
      "rose",  V4d (1.0, 0.45, 0.6, 1.0) ]

let private tintIdx  = cval 0
let private speed    = cval 1.0          // turns per ~12s at 1.0
let private angle    = cval 0.0

let private tint     = tintIdx |> AVal.map (fun i -> snd tints.[i % tints.Length])
let private tintName = tintIdx |> AVal.map (fun i -> fst tints.[i % tints.Length])

/// Per-frame spin step, driven from `RenderControl.OnRendered` — no
/// requestAnimationFrame anywhere: the angle write marks the scene,
/// which schedules the next frame (the render-feedback loop the
/// portable controllers use too). dt comes from a wall clock so the
/// integration follows real time.
let private spinStart = System.DateTime.UtcNow
let mutable private spinLast = 0.0
let private spinTick (_e: RenderControlEventInfo) =
    let t = (System.DateTime.UtcNow - spinStart).TotalSeconds
    let dt = max 0.0 (min 0.1 (t - spinLast))
    spinLast <- t
    transact (fun () ->
        angle.Value <- angle.Value + dt * 0.5 * speed.Value)

// ─── Scene: a spinning box with the custom effect ─────────────────────

let private scene : ISceneNode =
    let spin = angle |> AVal.map (fun a -> Trafo3d.rotation (V3d.create 0.0 0.0 1.0) a)
    sg {
        Sg.Effect stripeEffect
        Sg.Uniform ("Tint", tint)
        Sg.Trafo spin
        // Aardvark.Dom-style ready-made primitive (mesh + Model trafo +
        // Colors attribute) — no Sg.Adapter boilerplate.
        Primitives.Box (V3d.create 1.0 1.0 1.0)
    }

// ─── App: renderControl + adaptive DOM overlay ────────────────────────

let app : App = fun ctx ->
    ctx.SetTitle "Aardvark.Portable demo"

    let cam =
        OrbitController.attach
            { OrbitController.defaults with
                InitialCenter   = V3d.zero
                InitialDistance = 5.0 }
            ctx.Window.Size

    div {
        Dom.Style "width:100%; height:100vh; touch-action:none; user-select:none; position:relative; background:#16181d"
        renderControl {
            Dom.Style "width:100%; height:100%"
            // Per-frame spin step — the Aardvark.Dom way to animate
            // (RenderControl.OnRendered), replacing the old rAF loop.
            RenderControl.OnRendered spinTick
            cam.Attributes
            cam.Camera
            cam.Frustum
            scene
        }
        // Overlay UI — portable DOM with adaptive bindings.
        div {
            Dom.Style "position:absolute; top:1rem; left:1rem; color:#eee; font-family:system-ui, sans-serif; background:rgba(0,0,0,0.45); padding:0.8rem 1rem; border-radius:0.5rem"
            h3 {
                Dom.Style "margin:0 0 0.5rem 0"
                "Aardvark.Portable demo"
            }
            p {
                Dom.Style "margin:0.2rem 0"
                tintName |> AVal.map (sprintf "tint: %s")
            }
            p {
                Dom.Style "margin:0.2rem 0"
                speed |> AVal.map (sprintf "speed: %.2g×")
            }
            button {
                Dom.Style "margin-right:0.4rem"
                Dom.OnClick (fun _ -> transact (fun () -> tintIdx.Value <- tintIdx.Value + 1))
                "next tint"
            }
            button {
                Dom.Style "margin-right:0.4rem"
                Dom.OnClick (fun _ -> transact (fun () -> speed.Value <- speed.Value * 1.5))
                "faster"
            }
            button {
                Dom.OnClick (fun _ -> transact (fun () -> speed.Value <- speed.Value / 1.5))
                "slower"
            }
        }
    }

[<EntryPoint>]
let main _ =
    Shell.runApp app |> ignore
    0
