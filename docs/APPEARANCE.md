# Images, gradients and layer effects

VectorSpace 0.4 adds editable raster paints, retained gradient/filter resources, an effect-stack inspector and improved SVG paint interchange. All scene painting remains in the C#/SkiaSharp engine. File-picking uses host capabilities; no image-processing service or external editor is embedded.

## Explore the material studies

Use **Appearance playground** in the main menu or quick actions. Confirm replacement only after saving a copy of your current document. The original geometric study demonstrates Fill, Fit, Crop and Tile on editable layers, followed by a gradient, independent colored drop shadows and an inner shadow. Every paint and effect is ordinary editable document data.

## Place and replace an image

Choose **Place image** or press **Ctrl+Shift+K**, then select a PNG, JPEG or WebP. The workbench decodes the file, normalizes its orientation and embeds a PNG. It creates one selected rectangle in the viewport; large images initially display at no more than 640 design units on their longest side. The underlying raster keeps its imported resolution. Undo removes the placement as one transaction.

To use an image inside an existing shape, change a fill's type to **Image**, then **Choose image**. **Replace image** changes the raster while preserving placement and appearance settings. Image paints use the same geometry as solid/gradient paints: rounded rectangles, ellipses, paths and text can all clip a raster. Image paint alpha is multiplied by paint opacity once, then the layer's opacity is applied to its final composite.

| Mode | Placement |
|---|---|
| Fill | Aspect-preserving cover; rotation-aware scaling keeps the target's corners covered. |
| Fit | Aspect-preserving contain, with transparent letterboxing rather than an invented background. |
| Crop | Cover-based scale plus explicit zoom, rotation and normalized offset. Zooming below cover is permitted and can expose transparency. |
| Tile | Repetition at an explicit source-pixel scale, with rotation and normalized offset. |

**Zoom** is disabled for Fill/Fit because those modes derive scale from geometry. **Angle** affects all modes. Crop offsets are percentages of layer width/height, not pixels in the original asset. Exposure, contrast and saturation use one color-matrix filter; reset restores their neutral values. These controls are not a RAW development pipeline or a certification of Figma's image-adjustment formulas.

## Crop directly on the canvas

**Edit image crop** switches to Crop while preserving the current image's effective scale. A thirds grid replaces layer-resize handles. Drag inside the layer to translate its image without moving the layer. Wheel zoom keeps the image pixel beneath the cursor anchored. Geometry is evaluated in the layer's local coordinates, so translated/rotated/reflected ancestors do not change the intended direction.

Each drag is a baseline-relative transaction rather than a chain of accumulated edits. **Enter** finishes crop mode. **Escape** cancels the active drag and exits; already committed earlier drags remain undoable normally. Clicking outside finishes cropping. Selection changes, document replacement and history restoration invalidate the old crop target instead of editing stale node references. Layer zoom/pan and image zoom are separate operations.

## Fills and gradients

Paints support visibility, opacity, their existing blend modes and reordering. Native fill order is bottom-to-top; **Move fill up** raises a paint toward the end of the list. Complete fill-stack overrides are retained on linked instances; an image or gradient override is no longer reduced to a single solid color.

Linear/radial gradients support independently editable stop positions, colors and opacity, repeat/reflect/pad spread, endpoint coordinates, a gradient transform and normalized or user-space coordinates. Imported radial gradients can carry an explicit radius and focal point. Their inspector exposes radius/focal controls instead of displaying endpoint controls that would have no effect. Legacy native circular Start/End radial paints retain their old geometry.

The renderer applies shader alpha and fill opacity independently, so a transparent fallback solid color does not erase a gradient. Stop changes invalidate the shader; movement or fill-opacity changes do not require rebuilding it. Stop order is sorted for drawing without changing authoring order.

## Layer effects

The Effects section adds, changes, hides, removes and reorders effects. Supported types are **DropShadow**, **InnerShadow** and **LayerBlur**. Shadow controls include color, opacity, offsets, blur and signed spread. Positive spread expands an outer shadow's alpha mask; inner-shadow spread erodes the occluding alpha mask. Blur is represented as twice the Gaussian standard deviation. Spread uses Skia's rectangular morphology kernel; this is not a signed-distance geometric offset operation.

All outer shadows sample the original source content, not the output of preceding shadows. The content is composited above them. Inner shadows are composited atop the original content with its alpha preserved, and do not tint the outer shadows. Layer blur then processes the resulting layer, including descendants. Within each shadow category, the first visible effect is topmost. Arbitrary interleaving of blur/shadow operations and Figma's complete effect ordering/blend behavior are not claimed.

Native effect descriptors retain the historical `Shadows` collection name for migration compatibility. A missing effect kind in older files means DropShadow. Filters are retained across unchanged frames and invalidate on descriptor changes or a changed processing footprint.

## Resource ownership and limits

`ImageAssetCache` is a single-render-thread LRU with both entry-count and decoded-byte budgets (128 entries and 128 MiB by default). Equal embedded payloads share one decoded image even after document cloning produces a new string object. Per-string digest memoization uses weak keys, so the lookup accelerator does not retain old documents. Returned images are borrowed: do not dispose them or retain them across cache mutation. Reducing a budget evicts immediately; increasing it allows formerly oversized assets to be retried. Bounded negative caching avoids repeated decoding of a damaged payload.

Gradient and effect caches use bounded insertion-order eviction, with defaults of 512 shaders and 256 filter graphs. They retain primitive descriptors rather than whole document or image-paint objects. Deleted-node resources are trimmed with the geometry cache. Image shaders remain short-lived so an old shader cannot secretly retain a decoded image after eviction. There are still per-draw managed/native allocations; this is not allocation-free rendering.

Imports are limited to 8 MiB encoded bytes, 8192 pixels per edge and 16 megapixels. Dimensions are checked before pixel allocation. Animated images are rejected rather than silently converted to their first frame. A normalized PNG must also fit the encoded limit. Native document/clipboard text is limited to 32 MiB; repeated embedded payloads in the serialized scene and overrides count toward that limit even though decoded rendering storage is deduplicated. A document-level binary asset archive and compression-aware history are not added here.

Native JSON and SVG parsing validate embedded raster MIME/header/size information without depending on Skia. Header plausibility alone does not prove valid compressed pixels; actual decoding can fail. The raster renderer skips an invalid image and exposes its error through `ImageAssetCache.LastError`; it never dereferences HTTP/file/SVG URLs. The normal image-picker path fully decodes and normalizes before committing the edit.

## SVG interoperability

SVG import now resolves local linear/radial gradient references and bounded/cycle-checked inheritance, stops, opacity, spread, transforms, user-space coordinates and radial focus. Inline style takes precedence over a presentation attribute. CSS hexadecimal alpha ordering is translated into the engine's native ARGB representation. Root viewBox alignment and group scaling are baked through descendants, including user-space gradient coordinates.

Embedded raster `<image>` elements become editable image paints. External image references, scripts, foreign objects and DTDs are not fetched or executed. Unsupported paint servers and cyclic references are reported. SVG export retains embedded images, placement, tiling patterns, vector clips, color-matrix adjustments, gradient definitions and the supported effect graphs.

SVG is **not** a lossless native document format. Imported path bounding boxes, full CSS color/style coverage, use references, referenced clips/masks, filter-graph import, nonuniform typography scaling, arbitrary skew and complete nested viewport semantics remain limited. Effect export regions for unclipped containers use nominal geometry plus padding and can clip extreme child overflow. Embedded images supplied directly through the portable SVG API are not re-encoded by that API; hosts needing canonical PNG/orientation data should normalize them through `RasterImageCodec.Import`. ICC/HDR/Display-P3 fidelity and lossless image metadata preservation are not certified.

## Reuse

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Skia;

var imported = RasterImageCodec.Import(File.ReadAllBytes("picture.jpg"));
var layer = new DesignNode
{
    Width = 640, Height = 400, CornerRadius = 24,
    Fills = [new()
    {
        Kind = FillKind.Image, ImageData = imported.DataUri,
        ImageMode = ImageScaleMode.Crop, ImageScale = 1.25,
        ImageOffset = new Vec2(.1, 0), Saturation = -.2
    }],
    Shadows = [new() { X = 0, Y = 12, Blur = 24, Opacity = .2 }]
};
using var renderer = new SceneRenderer();
renderer.Images.ByteBudget = 64L * 1024 * 1024;
File.WriteAllBytes("preview.png", renderer.ExportPng([layer], layer.LocalBounds.Inflate(48)));
```

`ImagePlacement.Calculate` is backend-independent in Core; embedded header validation and SVG conversion are in Documents; decoding, caches and filter graphs are in Skia. Reusable workbench hosts implement `IWorkspaceStorage.OpenImageAsync`. Its default implementation reports unsupported image picking, so existing hosts remain source-compatible without presenting a fake picker.

The format is now **schema 4**, with schema-1/2/3 migration. Older applications must reject schema 4 rather than silently lose images or effects. See [validation](VALIDATION.md), [performance](PERFORMANCE.md) and the [remaining feature boundary](FEATURES.md).

Behavior/API references: [Figma paint definitions](https://developers.figma.com/docs/plugins/api/Paint/), [Skia image filters](https://api.skia.org/classSkImageFilters.html), and [SVG paint servers](https://www.w3.org/TR/SVG2/pservers.html). These are terminology and implementation references, not binary compatibility or pixel-parity certification.

## Admission and budget guarantees

`EmbeddedImage.Inspect` exposes immutable MIME, dimensions and encoded-byte metadata, weakly memoized by payload identity. Image cache misses use the metadata to reject over-budget rasters **before invoking pixel decoding**. The codec overload accepting `maxDecodedBytes` independently validates the actual codec dimensions before bitmap allocation. Admission evicts older decoded entries first; temporary codec buffers and orientation surfaces still exist outside the resident-image budget.

Content digests hash the existing UTF-16 span, avoiding an image-sized UTF-8 array when history restores equal content in a new string. The digest is process-local and is not a serialized asset identifier. Oversized URI strings are rejected before hashing and are not negatively cached. Base64 decoding consumes the original character span rather than allocating a payload substring; bounded whitespace-compatible fallback resizing remains possible.

Decoded bitmaps are marked immutable before producing their `SKImage`, allowing Skia to share their pixel storage when supported. The returned image retains its pixels after codec/bitmap temporaries are disposed. This is a resource-ownership contract, not a guarantee that every native backend avoids every copy. Reference: [Skia raster images from bitmaps](https://api.skia.org/namespaceSkImages.html).

Reducing image, gradient or effect cache limits evicts excess entries immediately. All capacities require at least one entry; the decoded-image byte budget can be zero. Budget increases allow retry of previously oversized images. `DecodeAttemptCount`, resident counts and successful decode/build counters distinguish admission rejection, failed decodes and resource reuse.

Seventeen admission regressions cover metadata, zero/tight budgets, codec overload compatibility, pixel lifetime, equal-content lookup allocation, oversized keys, Base64 validity and dynamic native cache limits. Allocation assertions exclude construction of the input payload and native allocations. The full engine suite contains 310 tests; exact-commit CI results remain the evidence of a passing build.
