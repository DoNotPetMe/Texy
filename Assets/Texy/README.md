# Texy — VRChat Avatar Texture Generator

Texy is a professional Unity Editor extension for generating avatar textures **entirely inside the
Unity 2022 editor that VRChat uses** (Unity 2022.3 LTS). Give it a prompt and it paints fully custom
textures; give it nothing and it auto‑generates textures from your avatar's geometry. It produces
every common map type — **Albedo, Normal, Emission, Decal/Tattoo, Metallic, Roughness, Ambient
Occlusion, Height, Smoothness** — and imports each one with the correct VRChat settings.

It ships with two engines:

| Engine | Needs setup? | Best for |
| --- | --- | --- |
| **Procedural (offline)** | No — works out of the box | Patterns, materials, PBR sets, instant iteration, zero cost |
| **AI (prompt‑to‑image)** | An image endpoint + key | Photoreal / highly specific prompt results |

Both engines share the same UI, the same geometry awareness, and the same import pipeline.

---

## Install

1. Copy the `Assets/Texy` folder into your VRChat avatar project's `Assets/` directory.
2. Unity compiles the editor scripts (an assembly definition keeps them editor‑only).
3. Open **Tools ▸ Texy ▸ Texture Generator** (shortcut `Ctrl/Cmd + Shift + T`).

No external packages are required. The optional AI engine uses Unity's built‑in networking only.

---

## Quick start

1. **Target** – Drag your avatar mesh (the GameObject with the Skinned Mesh Renderer) into the
   *Avatar Mesh* field, then click **Analyze Geometry**. Texy reads vertex/UV/island stats, guesses
   the surface type, and derives a stable per‑mesh seed. The *Material* field auto‑fills.
2. **Prompt** – Type something like `iridescent emerald dragon scales, detailed`. **Leave it empty**
   to auto‑generate a texture that fits the geometry.
3. **Maps** – Toggle which maps to create, or use **Full PBR Set** / **Albedo Only**.
4. **Generate** – Texy creates the maps, saves them to `Assets/Texy/Generated`, applies the correct
   import settings, and assigns them to the material.

### How "smart, geometry‑aware" generation works

- **Mesh analysis** (`MeshAnalyzer`) computes UV coverage, island count, bounds aspect, an existing
  texture size to match, and a deterministic geometry seed, then classifies the surface
  (body skin / clothing / hair / hard‑surface / accessory).
- **UV island mapping** (`UVIslandMapper`) rasterizes the mesh's UVs into a coverage mask and a
  per‑island tone map, so decals/tattoos only land on real surface and parts read distinctly.
- **Prompt parsing** (`PromptParser`) turns text into a structured style — palette, material family,
  pattern, roughness/metallic/emissive hints — used by both engines for consistent results.

### Map correctness (why VRChat textures look right)

`TextureImportPipeline` sets the things people usually get wrong:

- Albedo / Emission / Decal → **sRGB**; Normal / Roughness / Metallic / AO / Height → **linear**.
- Normal maps imported as **Normal map** type; decals keep their **alpha**.
- Tiling = Repeat, mipmaps + streaming on, sensible max size and compression.

`MaterialApplier` assigns each map to the right slot and enables the needed keywords for both the
**Unity Standard** shader and **Poiyomi** (probing known property names, degrading gracefully).

---

## AI engine setup (optional)

Switch **Engine** to *AI* and open *AI endpoint settings*:

- **OpenAI‑compatible** (`/v1/images/generations`): set the endpoint, model, and API key. Texy sends
  `response_format: b64_json` and decodes the image (URL responses are downloaded automatically).
- **SD‑WebUI** (Automatic1111 / **Forge** `/sdapi/v1/txt2img`): set the endpoint; no key needed for local
  servers. This is the recommended free path on a local GPU. Texy passes `tiling` (driven by the
  *Tileable* checkbox) so results are seamless, plus a texture‑oriented negative prompt. Whatever
  checkpoint is loaded in the WebUI is the model Texy uses.

### Retexture mode & source art

For a finished avatar, generating a fresh tile over its UVs produces visible seams. Enable
**Retexture mode (img2img)** so Texy restyles the avatar's *existing* texture instead — preserving the
UV layout, placement and hidden seams. The **Denoise** slider trades "keep the original" (low) against
"restyle harder" (high); 0.5–0.6 is the sweet spot.

For best fidelity, drag the avatar's original **PNG or PSD** source art into **Source texture
(optional)** under *Options*. Texy uses that as the img2img base (and for deriving maps) instead of the
compressed in‑game texture — sharper results, fewer artifacts. The source must match that material's UV
layout (i.e. it's the original art for the same texture). Unity imports PSDs natively, so they work directly.

Color maps come straight from the model with map‑specific prompt engineering. Data maps
(normal/AO/height/roughness/metallic) are derived on‑device from an AI‑generated albedo so the whole
set stays consistent. The API key is stored locally in `EditorPrefs` and is never committed.

---

## Determinism & iteration

Every generation is driven by a seed. **Same seed + same prompt + same map = identical output**, so
you can iterate on one map and reproduce the rest. The 🎲 button randomizes the seed; assigning a mesh
mixes in its geometry seed automatically.

---

## Project layout

```
Assets/Texy/Editor/
  Core/        MapType, TextureRequest/Result, settings, logging
  Generation/  provider interface, prompt parser, noise, pattern renderer,
               procedural engine, AI engine, map post‑processors
  Geometry/    mesh analysis + UV island mapping
  Pipeline/    import settings + material assignment
  UI/          the editor window
```

Everything is namespaced `Texy` and built behind `Texy.Editor.asmdef` (Editor platform only).
