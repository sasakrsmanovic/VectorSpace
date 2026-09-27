from pathlib import Path
p = Path('tests/VectorSpace.Tests/PrototypeTests.cs')
s = p.read_text().replace('new() { TargetId', 'new PrototypeAction { TargetId').replace('new() { Kind = PrototypeActionKind', 'new PrototypeAction { Kind = PrototypeActionKind')
p.write_text(s)
