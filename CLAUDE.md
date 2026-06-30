# Photo2PCB

A utility for converting a head photo into a stylized portrait built from straight horizontal lines — the look of a face etched as PCB copper traces — and exporting it as an SVG for etching to PCB copper.



### Requirements

* Develop in C# using VS Code

* Cross-platform (Windows, Linux, Mac)

* Takes JPG or PNG image file as input.

* Generates SVG as output.

* Creates a new SVG black-on-white as output.

* Shows a preview of what the output will be based on user settings.

* User settings (presented as slider controls on the main form):
  
  * Contrast Ratio (0% to 100%, default 50%)
  
  * Brightness (0% (dark) to 100% (white), default 50%)
  
  * Background Cutoff (0% to 100%, default 3%) — strokes lighter than this are dropped to clean white. Raise it to remove a light background and isolate the darker subject; leave it low for a full-field scanline raster.
  
  * Min Aperture — the line width used for the lightest strokes (0.127mm/5mil to 1.27mm/50mil, default 0.127mm/5mil)
  
  * Max Aperture — the line width used for the darkest strokes; darkness modulates the stroke width between Min and Max (must be ≥ Min, default 0.508mm/20mil)
  
  * Image Height — the overall output height in millimetres (10mm to 500mm, default 100mm). The width follows the source photo's aspect ratio.
  
  * Line Count — the number of horizontal lines used to approximate the image (default 150). The maximum is capped at `floor(2 × Height ÷ MaxAperture)` so the darkest lines cannot pile up beyond ~2× coverage; fewer lines = coarser/more stylized, more = finer detail.

* Upon change of any setting, the preview image shown to the left is updated.

* File drop-down menu has
  
  * File Open (filters for .PNG and .JPG files)
  
  * File Save and Save-As options. First save of a new output is ALWAYS Save-As and will not overwrite the input file unless the user explicitly does so.
  
  * Exit for closing the program

* Utility can be called from command-line:
  
  * Input file, Output file, and user settings other than default may be specified with command line switches. 
  
  * A switch for non-interactive mode will cause the utility to generate the output file according to command line switches without opening any dialog or showing the preview.
  
  * Dialog will open with settings and preview according to CLI options if the non-interactive switch is NOT invoked. 
  
  * If the utility is executed with no switches or with no files specified, opens the dialog waiting for user to specify input file with File >> Open menu item.
- Visual Requirements of dialog:
  - Adopts host OS color theme (Dark mode, light mode)
  - Generated Image Preview is shown on the left 2/3 of the dialog.
  - Dialog can be expanded and shrunk, with minimum size 640x400 pix
  - Dialog supports host OS DPI scaling.
  - Opens with equivalent to 800x600 at 96 DPI size.
  - Right 1/3 has horizontal slider controls with labels matching.
  - Bottom-Right has Save As... and Cancel buttons.



### What this utility does

Along with user specified inputs and outputs, the core function of the utility is to convert any input image (photo) into a stylized black-on-white portrait made of straight horizontal lines of fixed width (specified by Aperture size). The result must read as the original person, rendered in a PCB-trace motif — straight lines put together to form the photo, with no dithering noise, moiré, or other artifacts.

The conversion is a **variable-aperture horizontal raster**, like a photoplotter flashing a circular aperture along each scan line where the aperture swells in dark areas and thins in light areas. It is NOT dithering:

1. If the input image is color, it is first converted to grayscale.

2. The output canvas is sized by the user-specified **Image Height**; the width follows the source photo's aspect ratio (`width = height × sourceWidth / sourceHeight`).

3. The image is divided into **Line Count** evenly-spaced horizontal lines. Each line is sampled along X at the minimum-aperture pitch, and each sample's tone is found by **area-averaging** (a clean box-resample) — this keeps the output free of dither noise and moiré.

4. Brightness and Contrast are applied to the per-sample tone.

5. Each sample's **line width is set by its darkness** — Min Aperture for the lightest strokes up to Max Aperture for the darkest. Consecutive samples of (nearly) equal width are merged into round-capped runs, so each scan line is a **continuous variable-width horizontal trace** (an unbroken line that swells and thins). There are no short dashes to align vertically, so the image reads as horizontal lines, not a grid. Truly white areas leave a gap, giving the trace a rounded end (photoplotter aperture).

The background of the generated image is always white. All strokes are drawn with **rounded end caps**, as if flashed by a fixed circular aperture.

The vertical line pitch is `Image Height / Line Count`. Because the stroke width varies with darkness, the spacing between lines varies too: light areas leave `pitch − MinAperture` of white, while dark areas leave `pitch − MaxAperture` (which may be negative, i.e. the heavy strokes overlap into solid black). Choosing `MaxAperture < pitch` therefore guarantees white space between every line. Increasing the line count at a fixed height packs the lines tighter rather than adding white space. The line count is capped at `floor(2 × Height ÷ MaxAperture)` to bound how far the darkest lines may overlap. The SVG is dimensioned in real-world millimetres (with a matching mm-unit `viewBox`) so it can be used directly in PCB CAM / etching workflows.




