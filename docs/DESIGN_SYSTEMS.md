# Local design systems

## Variables and modes

Open **Local variables** from the main menu or `Ctrl+K` quick actions. Create a collection, then color, number, string or boolean variables. Every mode has a value for every variable. Adding a mode copies the collection's default values. Choose the editing mode in the dialog before editing its values. Use a variable's alias dropdown to reference another variable of the same type, including another collection. Deleting the last mode is prohibited. Deleting a variable with dependents is rejected rather than leaving broken aliases.

Select a layer and scroll to **Variables**. Choose a target property and a compatible variable. The binding affects the actual scene property, not just an inspector label. Color fills/strokes, text, font family, visibility, dimensions, opacity, radius, typography spacing and layout gaps/padding are supported. A width/height binding disables that dimension's fill/hug sizing. Geometric values are clamped to the model's supported range.

Mode precedence is the nearest explicit layer/ancestor mode, then document mode, then collection default. **Inherit** removes a local choice. Aliases resolve using the target collection's effective mode. The union of all mode-specific alias edges must be acyclic; this is intentionally conservative and can reject graphs that are acyclic in only some mode combinations. Alias depth is bounded at 64; collections, modes and variables have validation limits.

Unbinding keeps the resolved appearance. The engine API can explicitly restore the captured pre-binding fallback. Unbinding inside an instance records a literal override so later source synchronization cannot silently reattach the source binding. **Reset instance overrides** restores source-driven values. Deletion and all authoring actions support undo/redo.

```csharp
using VectorSpace.Core;
using VectorSpace.Editing;

var theme = VariableService.CreateCollection(session, "Theme");
var dark = VariableService.AddMode(session, theme.Id, "Dark");
var surface = VariableService.Create(session, theme.Id, "Surface", VariableValue.Color("#FFFFFF"));
VariableService.SetValue(session, surface.Id, dark.Id, VariableValue.Color("#111111"));
VariableService.Bind(session, layerId, VariableTarget.Fill, surface.Id);
VariableService.SetMode(session, theme.Id, dark.Id, frameId);
```

Resolve identifiers at execution time after undo/redo: snapshot restoration replaces model object references. Public service methods do this internally. Custom model edits must use `EditorSession.Edit`; arbitrary direct writes outside a transaction do not publish document-change notifications.

## Component sets and variants

Create a component with `Ctrl+Alt+K`. **Add variant** wraps a lone component into a set and adds a second definition. Alternatively select unlocked sibling components and use **Combine as variants**. Definition IDs and world placement are retained. Sets have a dashed purple outline and contain only component definitions.

Choose a definition and edit its variant values in the inspector. The engine's `ComponentVariants.SetProperty` adds named axes to the set; duplicate combinations are rejected. **Assets** displays one tile per set and inserts its top-left default definition. On a selected instance, the **Component properties** section switches variant values. With multiple axes, it prefers the target matching the greatest number of the other current values; this deterministic rule is not claimed to reproduce every Figma fallback rule.

```csharp
var set = ComponentVariants.Combine(session, "Button");
ComponentVariants.SetProperty(session, firstDefinitionId, "State", "Default");
ComponentVariants.SetProperty(session, secondDefinitionId, "State", "Pressed");
ComponentVariants.SwitchProperty(session, instanceId, "State", "Pressed");
```

Swapping matches descendants by hierarchical name, kind and sibling occurrence, rather than guessing from absolute position. Matched text/fill overrides and runtime IDs are preserved; unmatched overrides are dropped. An instance that still has the old definition's dimensions adopts the new definition's dimensions; explicitly resized dimensions are retained. Nested component scopes are kept separate to prevent duplicate IDs when the same component occurs more than once.

Synchronization detects definition cycles and excessive expansion before cloning. Source fingerprints include transitive nested dependencies, while unchanged instances skip rebuilding. This is local document synchronization, not a multiplayer transport or a remote-library service.

## Files and clipboard

Native document format **2** stores collections, values, aliases, mode choices, bindings and variants. Version-1 documents load and migrate to version 2; old applications may reject newly saved files. Back up important files before migrating. New clipboard envelopes carry variable dependencies. Same-document paste reuses available variable identities; cross-document paste imports dependencies with new collection/mode/variable IDs and remaps aliases/bindings. Legacy node-array clipboard payloads remain readable.

SVG and PNG preserve supported appearance, not editable variable/variant semantics. Native `.fig` interoperability, variable expressions, exposed component properties and remote design libraries remain separate unimplemented capabilities.

## Reference behavior

The implementation uses public documentation as a behavior reference, not Figma source or assets: [variables](https://help.figma.com/hc/en-us/articles/15339657135383-Guide-to-variables-in-Figma) and [variants](https://help.figma.com/hc/en-us/articles/360056440594-Create-and-use-variants).
