# Property transfer, batch naming and layer order

VectorSpace 0.7 adds reusable style-transfer and naming services, custom workbench panels, stable layer ordering and a completed-save keyboard-focus boundary. These extend the existing drawing, point editing, layout and collaboration engines; they do not replace them with a separate editor.

## Copy and paste properties

Select a source layer and use **Ctrl+Alt+C** or **Copy properties** in quick actions/the canvas menu. Select one or more targets and use **Ctrl+Alt+V**. The most recently selected source supplies the snapshot. The operation changes supported properties, not node identity, position, dimensions, rotation, text content, children or prototype links.

| Group | Copied data |
|---|---|
| Fills | All paints, visibility, opacity, blending, gradient stops/transforms and embedded image placement/adjustments. |
| Strokes | Stroke colors, opacity, width, visibility and dash patterns. |
| Effects | Independent outer/inner shadows and layer blur, with their complete supported descriptors. |
| Typography | Whole-layer family, size, weight, alignment, line height and letter spacing, only from text and only onto text. |
| Appearance | Layer opacity, blending and corner radius where both source and target support corners. |

Use **Paste selected properties** to choose groups in the dedicated panel. **Copy fills/strokes/effects/typography** and corresponding paste actions transfer a single group. A copied empty list intentionally clears that group; an omitted group leaves it unchanged. Locked targets are skipped. Explicitly selecting a parent and its child applies styles to each selected node; this is distinct from movement, where descendants must not be transformed twice.

Property transfer copies currently resolved values. It does not import a source variable collection or create a hidden reference to another document. Only the transferred target properties are unbound; width, height, content and unrelated bindings remain intact. Inside an instance, an explicit disabled local binding prevents inherited variables from replacing the newly pasted literal value during synchronization.

A clipboard packet is plain text beginning with `VectorSpace.Properties/1`. It contains styles and a short source label, not children or geometry. Native input and embedded-image validation applies before a transaction begins. The packet is capped at 32 MiB of text. No scripts, expressions or remote image URLs are executed. A readable unrelated system clipboard is never silently replaced by an old internal style copy. When system clipboard access itself fails, the current window can use its own last property snapshot.

## Persistent instance editing

Stroke stacks, whole-layer typography, opacity, blend, supported corners and layer names now have explicit instance override records in addition to existing text/fill/effect overrides. The normal inspector uses the same typed property mutation path, so changing a supported font or stroke on an instance descendant persists after its definition changes.

Structurally matched variant swaps retain these records and stable descendant IDs. Reset instance overrides restores inherited descendant style/binding state through normal component synchronization. This does not add arbitrary structural edits, every exposed component property, all root transform-reset semantics or published remote libraries. Reordering an instance's children is rejected with an instruction to edit the main component or detach the instance; it is not silently undone by synchronization.

## Batch rename

Select unlocked layers and press **F2**, use **Ctrl+R** in a host that permits it, or invoke **Rename layers**. The panel places a bounded before/after preview next to Match and Rename to fields. It previews eight rows, reports the complete changed-target count, and disables Apply for invalid or no-op input. Preview requests are debounced; apply performs a fresh validation rather than trusting a stale preview.

Numbering follows the actual layer panel: front-to-back sibling order, parent before descendants. Explicitly selected descendants are included; locked selections are excluded before numbering.

| Pattern | Meaning |
|---|---|
| `$&` | Current complete name, or current matched portion when Match is supplied. |
| `$n`, `$nn`, `$nnn` | Ascending number with minimum width of one, two or three digits. |
| `$N`, `$NN`, `$NNN` | Descending number; Start defines the final/lower number. |
| `$1`, `$2`, … | Regular-expression capture group. |
| `$$` | Literal dollar sign. |

For example, **Rename to** `Card $nn — $&`, with Start `1`, prefixes selected names with padded ascending numbers. With regular expressions enabled, **Match** `(Icon)_(\d+)` and **Rename to** `$2/$1` changes `Icon_003` into `003/Icon`. With regular expressions disabled, Match is literal; metacharacters are escaped. An empty Match replaces each whole name, while a nonmatching name stays unchanged.

Regular expressions use .NET's linear-time non-backtracking engine with a timeout. Lookarounds and pattern backreferences are deliberately rejected; they are not run through an unbounded fallback engine. The replacement supports bounded capture indices and number padding up to nine digits. Names are limited to 4096 characters, match input to 512, and total changed-name output to 16 MiB. Empty/control-character names and oversized expansions are rejected before mutation.

The plan records IDs and previous names. Apply resolves current nodes and checks every target still exists, remains editable and retains its original name before changing any of them. One rename is one undoable/shared transaction. A stale peer rename cannot be overwritten using an old modal preview. Individual layer-row rename uses the same guarded service and captures a name override on instance descendants.

## Stable layer order

Bring forward/backward moves each selected run over one unselected neighbour without reversing selected peers. Bring to front/send to back uses a stable partition: relative order within the selected and unselected sets is retained. The operation handles each parent independently and keeps selection IDs unchanged. Already-extreme selections do not serialize a no-op undo snapshot.

The extreme operation is O(n) per affected sibling list and allocates one temporary array of that list's length. This replaces repeated linear searches and element shifts. It is not allocation-free, and whole-document history/validation/layout still run for a committed edit. Moving children in an auto-layout container intentionally changes their flow order; the same layout engine arranges the new sequence.

## Save and inline-text focus

Save synchronously commits the active inline editor, completes inspector focus-loss edits before serialization, awaits the host download/picker operation, and restores canvas focus after save-state UI refresh. It does not steal focus from a subsequently started text/gesture transaction. The browser regression saves immediately after typing, verifies actual save completion and canvas focus, reopens with Enter, cancels only the second edit and downloads again.

The previous post-merge 0.6 run failed at reopening inline text after Save; adding a sleep to the test was not the fix. Read-only diagnostics now distinguish an in-flight save from an editable canvas focus state.

## Native format and shared-room upgrade

The current native format is **schema 5**, identified by `DesignDocument.CurrentFormatVersion`. Schemas 1–4 still load through migration. Newly created/opened editor documents save schema 5. Older apps reject schema 5 rather than silently losing new instance property records.

A 0.7 server replays an older room's journal first, then appends a durable system revision containing the native-format migration. It does not rewrite the original metadata, renumber existing revisions or delete request receipts. Restarting again does not repeat the upgrade. Historical versions still reconstruct; their downloaded native documents are migrated for the current reader. Back up the data directory and upgrade the browser/native clients together with the backend. Old clients may reject the new schema and need reloading; this is not a backwards-compatible live mixed-version protocol.

## Reusable API

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

string clipboard = PropertyClipboard.Copy(sourceNode);
var properties = PropertyClipboard.Read(clipboard).Properties;
PropertyTransfer.Paste(editor, properties, PropertyGroups.Fills | PropertyGroups.Strokes);

var names = LayerRename.SelectedTargets(editor);
var plan = LayerRename.Plan(names, new RenameOptions("Card $nn — $&"));
LayerRename.Apply(editor, plan);

editor.Reorder(direction: -1, extreme: true); // stable send to back
```

`LayerProperties` and `StyleCloner` live in Core; portable parsing/validation in Documents; transactions, names and ordering in Editing; presentation-only panels in Controls; shortcuts, clipboard and modal integration in Workbench. Mutable paint/stroke/effect lists are copied; immutable strings and embedded raster payloads are shared. Capturing one layer's properties does not clone or traverse its descendants. The normal native document serializer remains responsible for persistence and its file-size limits.

## Validation and boundaries

Engine coverage includes property ownership, paste isolation, bindings, instance synchronization/variants, migration, invalid input, exhaustive stable-order equivalence and rename preflight. Browser coverage uses actual keyboard, clipboard, file-picker and panel input; a separate real two-client case checks shared property paste and own undo preserving a peer's geometry. CI reports the exact source-commit result rather than deriving success from the test count.

Run `dotnet run --project tests/VectorSpace.Tests -c Release -- --benchmark-workflows` for the ordering/capture benchmark. It compares a correct stable remove/append reference with the new partition, verifies equivalent output and checks property-capture allocation with zero versus 10,000 descendants. Input setup, history, layout, validation, network and rendering are outside that timing. No application-wide FPS improvement is implied.

Whole-layer typography is not mixed rich text. This increment does not add native `.fig`, vector networks, shape builder, pressure strokes, plugin execution, remote libraries or every Figma property-copy/rename semantic. The UI is authored for the existing Uno workbench, not certified pixel-identical to all Figma screens.

Public behavior references: [Figma property transfer](https://help.figma.com/hc/en-us/articles/4412765442967-Copy-and-paste-properties-between-layers), [batch renaming](https://help.figma.com/hc/en-us/articles/360039958934-Rename-Layers), and [instance overrides](https://help.figma.com/hc/en-us/articles/360039150733-Apply-changes-to-instances).
