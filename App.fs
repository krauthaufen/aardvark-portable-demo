module Demo.App

open Fable.Core
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

/// requestAnimationFrame loop advancing the spin angle (dt-scaled).
[<Emit("requestAnimationFrame($0)")>]
let private raf (_cb: float -> unit) : unit = jsNative

let private startSpin () =
    let mutable last = 0.0
    let rec tick (now: float) =
        let dt = if last = 0.0 then 0.0 else (now - last) / 1000.0
        last <- now
        transact (fun () ->
            angle.Value <- angle.Value + dt * 0.5 * speed.Value)
        raf tick
    raf tick

// ─── Scene: a spinning box with the custom effect ─────────────────────

let private scene : ISceneNode =
    let spin = angle |> AVal.map (fun a -> Trafo3d.rotation (V3d.create 0.0 0.0 1.0) a)
    sg {
        Sg.Effect stripeEffect
        Sg.Uniform ("Tint", tint)
        Sg.Trafo spin
        Sg.Adapter (Primitives.box ())
    }

// ─── App: renderControl + adaptive DOM overlay ────────────────────────

let app : App = fun ctx ->
    ctx.SetTitle "Aardvark.Portable demo"
    startSpin ()

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
