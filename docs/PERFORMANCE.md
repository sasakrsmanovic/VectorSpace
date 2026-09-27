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
