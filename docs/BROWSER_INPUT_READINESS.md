# Browser join-input readiness

The editing-workflows 0.7 validation exposed a startup race in the collaboration
browser harness, not a failed viewer permission assertion. The failure trace
showed the managed control list already containing Join while the bootstrap
screen still covered the application. A coordinate click at that point did not
activate the displayed dialog, so the expected confirmation never appeared.

The harness now brings the joining page to the foreground and waits for a named,
enabled control with finite on-screen bounds. Its center must hit the rendered
canvas or a native input/control rather than a covering DOM element. Bounds must
also match across two distinct read-only diagnostic publications. Re-reading one
stale publication cannot satisfy readiness.

Input still goes through actual browser mouse and keyboard events. The helper
does not invoke commands, modify the document, retry a missed click, increase the
20-second control timeout or remove any viewer/commenter assertions.

Six standalone Node regression cases cover fresh publications, bootstrap
occlusion, moving modal bounds, disabled/offscreen controls, native text overlays
and an explicit timeout for blocked input. Build runs these separately from the
52 application browser cases. The multi-user cases still use the real compiled
service and independent browser contexts.

Passing the harness unit tests is not equivalent to passing the application
suite. The exact-commit Build, Desktop and public Pages checks remain separate
delivery gates. This change does not certify all native focus/accessibility paths
or complete Figma compatibility.
