# Performance and reproducible measurements

## Changes

`SnapIndex` snapshots visible nonselected targets and page guides once per gesture. It sorts and compacts edge/center coordinates in O(n log n), then queries predecessor/successor coordinates for the moving rectangle's three anchors in O(log n). Equal-distance ties preserve the previous linear engine's target/anchor ordering. Native regression tests compare 500 seeded randomized queries, including exact guide segments, against `SnapEngine`, which remains the reference implementation.

`SceneRenderer` retains bounded LRU path geometry and text-line measurements. Geometry is not discarded for a selection-only change or pure movement; shape/text edits invalidate their own keys. Conservative viewport rejection avoids clipping text/path overflow and unclipped containers. Native resources are explicitly disposed when evicted.

Component synchronization fingerprints definitions plus their transitive source dependencies and skips unchanged instances. Local overrides, nested identity scopes, cycles and expansion limits remain checked. Per-parent dictionaries replace quadratic descendant matching. Selection-only workbench updates retain page buttons and realized layer containers, and snapshot restoration validates selected IDs using one set rather than one scene walk per selected layer.

## Run the benchmark

```bash
dotnet run --project tests/VectorSpace.Tests -c Release -- --benchmark
```

The output is one JSON record. It uses 10,000 seeded target rectangles and 500 queries, warms both paths, measures five batches, and reports their medians. It verifies exact result equivalence before reporting a query-speed ratio. The setup/build cost is reported separately. No wall-clock threshold is enforced in tests.

An observed development run on Debian 13, x64, .NET 10.0.12 on September 27, 2026:

| Measurement | Result |
|---|---:|
| Index build, including cold setup/JIT effects | 99.6884 ms |
| Linear median, 500 queries | 549.1950 ms |
| Indexed median, 500 queries | 0.7881 ms |
| Observed query speed ratio | 696.86x |
| Linear allocated bytes per query | 2,880,216 |
| Indexed allocated bytes per query | 88 |

These are **synthetic CPU snapping-query results**, not a Figma comparison, end-to-end drag benchmark, GPU measurement or application-wide speedup. Index construction, UI work, rendering and history serialization are excluded from the query ratio. Rerun on the target device; JIT, workload distribution and hardware change the numbers substantially. The raw development record is [snap-index.json](benchmarks/snap-index.json).

## Remaining scalability limits

History still serializes before/after snapshots with entry and character budgets. Definition fingerprinting and document validation still visit document data at commit. Hit testing is not a full retained spatial acceleration structure; the viewport does not use damage-tile compositing. Large instance expansions, text shaping and large document load/startup need further profiling. No million-layer or fixed frame-rate claim is made.


## Appearance resource retention (0.4)

```bash
dotnet run --project tests/VectorSpace.Tests -c Release -- --benchmark-appearance
```

This separate CPU raster benchmark draws 48 layers with images/gradients and 16 two-shadow stacks into a 512×384 surface. It measures five batches of ten frames, comparing per-frame resource rebuilding against retention. PNG comparison is outside timing; identical pixels and zero additional image/gradient/effect constructions in the retained phase are asserted. Managed allocation measurements exclude native Skia memory. CI uploads its exact-run JSON alongside snapping results.

Development profiling exposed a different dominant cost: an inner-shadow recoloring filter that affects transparent black processed a viewport-sized region for every small layer. A `SaveLayer` bounds hint alone did not constrain that filter. Explicit processing crops for simple leaves/clipped frames reduced one local retained batch from **4817.31 ms to 311.108 ms for ten frames**. That is an implementation-development comparison for this particular new appearance workload, not a claimed speedup over the v0.3 application or Figma. Unclipped containers and uncertain text/path footprints remain conservative.

On that bounded development run, retained frames allocated **18,240 managed bytes/frame**, compared with **88,688 bytes/frame** when resource caches were cleared each frame, and built **zero** additional images, gradient shaders or filter graphs after warming. Retained vs rebuilt wall time was within noise (**311.108 vs 309.273 ms/batch**); the verified retention benefit here is resource reuse and reduced allocation, not a measured rendering speedup. Local hardware, native backends and CI measurements differ; the raw records are retained in `docs/benchmarks/appearance-*.json`.

Image cache budgets apply to decoded RGBA storage and entry count, not all process memory. Encoded strings, immutable snapshots, temporary codec buffers, SKPaint/shader instances and driver allocations still exist. SVG import/export and history can duplicate embedded strings; they are not O(1) with asset size. The renderer is single-thread-owned. See [appearance ownership and limits](APPEARANCE.md).


### Image admission and cloned payloads

The 0.4 finalization adds dimension-based admission before pixel decoding, a separately enforced codec pixel budget, direct-span content hashing and Base64 decoding, and immediate capacity reductions for gradient/filter caches. Seventeen regressions cover these contracts. A cloned 256x256 noisy PNG lookup must reuse the same decoded image and allocate under 16 KiB of managed memory; constructing the input and native memory are excluded. This checks bounded transient managed allocation, not constant-time hashing: a new string identity still requires reading its content once. See [appearance admission guarantees](APPEARANCE.md#admission-and-budget-guarantees).

## Collaborative projection and presence

The production projection uses a read-only `JsonDocument` and one reusable per-call UTF-8 writer rather than constructing a second mutable JSON tree. Span-based dictionary lookup reuses existing cell addresses; unchanged canonical property strings retain their original instances. A test-only copy of the prior projection independently verifies output across 59 equivalence/reuse cases, including Unicode, hierarchy reordering and seeded edits.

```bash
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release -- --benchmark
```

A local Linux x64/.NET 10.0.12 run used 1,000 native layers, one scalar edit and five interleaved warmed samples per implementation:

| Projection/diff measurement | Prior mutable JSON | Retained reader |
|---|---:|---:|
| Median elapsed | 99.524 ms | 48.3378 ms |
| Managed bytes allocated | 28,405,816 | 4,552,184 |

The full native document was 1,732,800 UTF-8 bytes; its exact one-cell edit batch was 209 bytes. The delta reconstructed the exact native document. [Raw local record](benchmarks/collaboration-projection.json); CI retains independent measurements rather than enforcing machine-dependent timing thresholds.

This measures projection/diff only: no network latency, journal flush, UI dispatch, render, native allocation or embedded-image workload is included. Projection still visits the entire document. A twofold observed projection speed ratio is not an application-wide speedup.

Presence does not enter undo history or trigger document projection. Client samples and room wakeups are coalesced; cursor painting uses a separate retained overlay. Unchanged follow-viewports do not cause another editor viewport notification, and remote-only revisions do not rewrite an unchanged local recovery journal. Full canonicalization, snapshots, retained revision/receipt state and recovery documents still consume workload-dependent memory. See [collaboration bounds](COLLABORATION.md).
