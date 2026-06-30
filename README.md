# Photo2PCB

Convert a head photo (PNG/JPG) into a stylized **portrait built from straight horizontal
lines** — the look of a face etched as PCB copper traces — and emit it as a black-on-white
**SVG** suitable for etching. The image is reduced to a set of evenly-spaced **continuous
horizontal traces whose width swells with darkness**: thin (min aperture) in the lights,
thick (max aperture) in the shadows, overlapping into solid where dark enough. It's like a
photoplotter flashing a circular aperture along each scan line. No dithering — tone is
sampled by area-averaging, so the result is clean (no moiré, no speckle, no vertical
banding), with rounded trace ends.

Cross-platform (Windows / Linux / macOS), built on **.NET 9** with an **Avalonia UI**
desktop front-end and a headless command-line mode.

## Layout

| Project | Purpose |
|---|---|
| [src/Photo2PCB.Core](src/Photo2PCB.Core/) | UI-free conversion engine: settings, rasterizer, dithering, SVG writer, preview renderer, CLI parser. |
| [src/Photo2PCB.App](src/Photo2PCB.App/) | Avalonia GUI + program entry point (CLI dispatch). |
| [tests/Photo2PCB.Core.Tests](tests/Photo2PCB.Core.Tests/) | xUnit tests driven by the sample images in [Test Files/](Test%20Files/). |

## Build, test, run

```sh
dotnet build Photo2PCB.sln
dotnet test  Photo2PCB.sln

# Launch the GUI
dotnet run --project src/Photo2PCB.App

# Headless conversion
dotnet run --project src/Photo2PCB.App -- \
  -i "Test Files/ChrisDenney-768x1024.jpg" -o out.svg \
  --height 80 --lines 150 --min-aperture 0.127 --max-aperture 0.6 \
  --contrast 60 --brightness 45 --no-gui
```

Run with `--help` for the full switch list.

## Settings

| Setting | Range | Default | Notes |
| --- | --- | --- | --- |
| Contrast | 0–100 % | 50 % | Scales around mid-grey; 50 % is neutral. |
| Brightness | 0–100 % | 50 % | 0 = dark, 100 = white; 50 % is neutral. |
| Background cutoff | 0–100 % | 3 % | Drop strokes lighter than this to white. Higher = whiter background / isolated subject. |
| Min aperture | 0.127–1.27 mm | 0.127 mm | Line width for the lightest strokes (5 mil). |
| Max aperture | ≥ min, ≤ 1.27 mm | 0.508 mm | Line width for the darkest strokes (20 mil). |
| Image height | 10–500 mm | 100 mm | Overall output height; width follows the photo's aspect ratio. |
| Line count | ≥ 10, capped | 150 | Horizontal lines approximating the image. Max = `floor(2 × Height ÷ MaxAperture)`. |

## How the conversion works

1. The output canvas is sized by the user's **Image Height**; the width follows the source
   aspect ratio (`width = height × sourceWidth / sourceHeight`).
2. The image is divided into **`LineCount` evenly-spaced horizontal lines**, sampled along X
   at the min-aperture pitch. Each sample's tone is found by an **area-averaging (box)
   resize** — no dithering, so no moiré or speckle.
3. Each sample's **line width** is set by its darkness — `MinAperture` for the lightest
   strokes up to `MaxAperture` for the darkest. Consecutive equal-width samples merge into
   round-capped runs, so each scan line is a **continuous variable-width trace** that swells
   and thins. White areas leave gaps with rounded ends.

The vertical line pitch is `Height / LineCount`. Because stroke width tracks darkness, the
**spacing between lines varies**: light areas leave `pitch − MinAperture` of white; dark
areas leave `pitch − MaxAperture` (negative ⇒ the heavy strokes overlap into solid). Pick
`MaxAperture < pitch` to guarantee white space between every line. Increasing the line count
at a fixed height packs the lines *tighter* rather than adding white space; line count is
capped at `floor(2 × Height ÷ MaxAperture)` to bound overlap. All strokes have round end
caps (photoplotter aperture). The SVG is dimensioned in real-world millimetres (with a
matching mm-unit `viewBox`) so it drops straight into PCB CAM / etching workflows.

## Notes

- `SixLabors.ImageSharp` carries one outstanding *moderate* advisory
  (GHSA-rxmq-m78w-7wmc) with no fixed 3.x release at time of writing; revisit when a
  patched version ships.
- Dithered photos at full resolution can produce large SVGs (one stroke per ink run).
  Run-merging / decimation is a natural future optimization.

## License

[MIT](LICENSE) © 2026 Ben Jordan
