# Prototyping

VectorSpace 0.3 introduces a private, deterministic prototype runtime, a reusable Uno player, a Skia presentation compositor, and an interaction inspector. Playback is part of the actual application, not an HTML overlay mockup. This is an independent implementation, not a Figma plugin runtime or a full Figma prototype-format implementation.

## Explore the editable playground

Open **Prototype playground** from the main menu or quick actions. The replacement confirmation protects your current work; save a local copy first. The sample contains editable flow frames, an overlay, named layers for smart interpolation, hover-driven color variables, and a small interactive component set.

Choose **Present prototype**. Click **Explore the details**, open and dismiss the overlay, hover the orb, or switch the state control. The details frame has a **Jump to notes** action and wheel/touch scrolling. Use the player toolbar to go **Back**, **Restart**, or **Close**. Escape dismisses the top overlay before exiting; R restarts and Backspace navigates back unless a matching authored keyboard reaction handles the key first.

## Author interactions

Select a layer, then choose the **Prototype** inspector tab. Frames expose a flow name and overflow axis. The plus button adds a reaction; each reaction contains an ordered action list. Action fields appear contextually. The inspector edits transitions, destinations, overlay placement/offset/backdrop, variable operations, typed comparisons, branches, and instance targets. Every edit is an ordinary undoable document transaction.

| Triggers | Semantics |
|---|---|
| Click | Release over the same interactive owner after a press; movement beyond six screen pixels cancels the click. |
| Mouse down / up | Explicit press/release events, distinct from click. A press that changes the active scene cannot activate the old scene again on release. |
| Mouse enter / leave | Entering/leaving the reactive ancestor path. These are explicit reactions, not an automatic reversible while-hover state. |
| Key down | A key or a chord such as `CTRL+SHIFT+K`, case-insensitive with spaces ignored. |
| After delay | A one-shot, bounded timer relative to the activation of the current input frame or overlay. |

| Actions | Supported behavior |
|---|---|
| Navigate / Back | Frame navigation with bounded history and optional retained scroll. Back dismisses an overlay before using navigation history. |
| Open / Swap / Close overlay | Modal overlays, placement presets/manual offsets, backdrop color/opacity and optional outside dismissal. No click-through. |
| Scroll to | Scroll to a descendant of the active frame/overlay within enabled overflow axes. Wheel and touch scrolling use the same coordinate system. |
| Set variable | Typed assignment, Boolean toggle or numeric addition in the runtime's private document and active mode. |
| Conditional | Typed equal/not-equal and numeric ordering, with recursively editable then/else action lists. |
| Change variant | Switch an explicit or nearest ancestor instance within its local component set, retaining supported overrides. |
| Open URL | Request an absolute HTTP(S) URL only from direct input. The workbench asks before launching. Timer and hover triggers never launch a website. |

Reactions bubble from the deepest hit layer to the first eligible owner within the active frame. The first matching reaction on that owner runs. Locked design layers remain interactive in preview; hidden layers do not. Transparent layers can act as hotspots. The top overlay alone receives input.

Missing destinations and missing variables remain repairable authoring references. Runtime execution reports an error, and a failed action batch leaves navigation, variables and component state unchanged. Legacy `PrototypeTargetId` links still work on click and can be upgraded in the inspector. An explicit click reaction takes precedence over a legacy link.

## Transitions

Instant, dissolve, move-in and push transitions are available, with direction, duration and linear/cubic easing. Smart animation prepares a matched working tree once. It matches sibling nodes by name, kind and occurrence, then interpolates position, size, shortest-path rotation, opacity, radius, supported solid fill colors and typography dimensions. Unmatched branches cross-fade as a whole; supported samples reuse the prepared node tree rather than cloning the document every frame.

Text-content changes and unsupported matched child paint/path changes cross-fade. Smart transitions involving overlays deliberately fall back to dissolve. This is not a general vector-network morphing engine: arbitrary path topology, complex paint interpolation, spring physics, fixed/sticky nested scrollers, media timelines, scrubbed on-drag transitions, and the full Figma smart-animation precedence rules are not implemented.

The target frame controls the presentation viewport during a transition. Geometry uses the existing Skia layout/text implementation; mixed rich-text/OpenType fidelity is not added by this feature. Pointer activation is suppressed during active transitions. The currently active final scene is the basis of a subsequent transition; arbitrary interrupted-animation continuity is not guaranteed.

## Isolation and lifecycle

`PrototypeSession` copies and validates the source document on creation. It never changes the editor's selection, viewport, save state or history. Ordinary navigation uses lightweight view state. Variable or variant action batches copy the private document once before editing; commit happens only after synchronization, layout and validation. Back changes navigation rather than undoing variable assignments; Restart reloads the initial private snapshot.

The player owns a monotonic `Stopwatch` and schedules a Uno dispatcher timer only for animation or the next pending reaction deadline. Unloading stops its clock; closing disposes its timer and input capture. An injected `SceneRenderer` is borrowed, not disposed, preserving host-installed fonts. The compositor temporarily disables edit outlines and restores renderer state afterward.

Limits: 64 reactions per layer, 32 actions per list, eight conditional nesting levels, 256 executed actions per event, 16 stacked overlays, 128 history entries, and 16–600000 ms delays. A newly entered scene starts its timers from the current clock. Re-entering a frame after modal dismissal restarts that input frame's delays. At most 64 due reactions are processed per clock update, and changing scenes stops processing old-scene timers.

## Reuse without Uno

`VectorSpace.Prototyping` is a ninth packable library and has no UI or Skia dependency. The application does not embed any executable script in a reaction.

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Prototyping;

var document = PrototypeSample.Create();
var playback = new PrototypeSession(document, "prototype-home");
var button = playback.View.Frame.Children.First(n => n.Name.StartsWith("Explore the details"));

playback.Dispatch(PrototypeTrigger.Click, button.Id, userInitiated: true);
playback.AdvanceTo(225); // Absolute monotonic elapsed milliseconds, supplied by the host.
Console.WriteLine(playback.View.FrameId);
Console.WriteLine(playback.Animation?.Progress(playback.ClockMilliseconds));
playback.AdvanceTo(500);
playback.Back();
```

Render the runtime through `PrototypeSceneRenderer` in `VectorSpace.Skia`; the host applies its fit/zoom transform once. `Hit` accepts frame-local coordinates and reports an outside-overlay click separately. `DrawPrototypeFrame` keeps the root background anchored while scrolling clipped descendants. Cached frame and overlay references avoid document-wide lookups on every animation sample.

```csharp
using VectorSpace.Editor;

var player = new PrototypePlayer(document, "prototype-home");
player.ExitRequested += () => { host.Content = null; player.Dispose(); };
player.StatusChanged += message => logger(message);
// Subscribe to LinkRequested only with an appropriate host consent/launch policy.
host.Content = player;
```

The runtime is single-threaded; callers must not mutate its exposed document while it is playing. `ComponentVariants.SwitchInDocument` is a low-level transaction primitive: standalone consumers must synchronize, validate and arrange before publishing changes.

## Persistence and dependencies

Native `.vectorspace` schema version 3 includes reactions, flow names, overflow and explicit local interaction overrides. Versions 1 and 2 migrate on load; old applications should reject version 3 instead of silently discarding its behavior. Newer feature support does not imply `.fig` import/export.

Internal node references are remapped when copying a connected subtree. Cross-document clipboard operations also remap action/condition variable references. Component definitions propagate interactions to instances unless an explicit local prototype override is set. Resetting inherited interactions reconnects the source. External destination frames not included in a copy remain unresolved references for repair.

## Verification and performance scope

The regression runner includes dedicated prototype tests for isolation, bubbling, locks/clips, scrolling, navigation history, overlays, typed variables and aliases, rollback, timer invalidation, budgets, URL policy, transitions, matching, pixel rendering, clipboard remapping and interactive variants. Browser tests use actual file-picker, pointer, keyboard and inspector actions, including saved-file isolation. Counts describe the suite; consult the Actions run for the exact tested commit.

No application-wide FPS or GPU acceleration claim follows from these tests. Smart tree preparation and variable/variant mutations still have document-dependent CPU/allocation costs. The implementation avoids full-document clones per animation frame; it does not claim allocation-free rendering or arbitrary-scale constant-time layout.

Public behavior references: [Figma reactions](https://developers.figma.com/docs/plugins/api/Reaction/), [prototype triggers](https://help.figma.com/hc/en-us/articles/360040035834-Prototype-triggers), [prototype actions](https://help.figma.com/hc/en-us/articles/360040035874-Prototype-actions). These references informed interaction terminology, not binary compatibility or pixel certification.
