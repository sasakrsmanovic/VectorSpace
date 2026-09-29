"""Final exact integration adjustments for migration and boundary validation."""
from pathlib import Path

def replace(path, old, new, count=1):
    p=Path(path); text=p.read_text(); assert text.count(old)==count, (path,old[:120],text.count(old),count); p.write_text(text.replace(old,new))

replace('tests/VectorSpace.Tests/PrototypeTests.cs', 'Equal(round.FormatVersion, 4)', 'Equal(round.FormatVersion, DesignDocument.CurrentFormatVersion)')
replace('tests/VectorSpace.Tests/ToolEditingTests.cs', "n.PathData.Count(c => c == 'L')", "n.PathData!.Count(c => c == 'L')")
replace('src/VectorSpace.Core/LayerProperties.cs', '''    public TextAlignment Alignment { get; init; }
    public static TypographyStyle Capture''', '''    public TextAlignment Alignment { get; init; }
    [JsonConstructor]
    public TypographyStyle(string fontFamily = "Inter", double fontSize = 24, int fontWeight = 400,
        double lineHeight = 1.25, double letterSpacing = 0, TextAlignment alignment = TextAlignment.Left)
    {
        FontFamily = fontFamily; FontSize = fontSize; FontWeight = fontWeight;
        LineHeight = lineHeight; LetterSpacing = letterSpacing; Alignment = alignment;
    }
    public static TypographyStyle Capture''')
replace('src/VectorSpace.Editing/LayerRename.cs', '''        var ids = plan.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);''', '''        if (plan.Any(p => p is null || string.IsNullOrEmpty(p.Id) || p.Before is null || p.After is null)) throw new ArgumentException("Invalid rename plan entry.");
        if (plan.Sum(p => (long)p.After.Length) > DocumentJson.MaxDocumentCharacters / 2) throw new ArgumentException("Batch rename exceeds the output budget.");
        var ids = plan.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);''')
replace('src/VectorSpace.Editing/LayerRename.cs', '''        editor.Edit(plan.Count == 1 ? "Rename layer" : "Rename layers", () =>''', '''        if (plan.All(p => p.Before == p.After)) return 0;
        editor.Edit(plan.Count == 1 ? "Rename layer" : "Rename layers", () =>''')
replace('src/VectorSpace.Editing/EditorSession.cs', 'var after = Capture(); var before = _before;', 'var after = Capture(); var before = _before;\n        if (after.Json.Length > DocumentJson.MaxDocumentCharacters) throw new InvalidDataException("The edit exceeds the native document size limit. No changes were committed.");')
replace('src/VectorSpace.Workbench/StudioWorkbench.PropertyTransfer.cs', 'if (PropertyClipboard.Read(text).Properties.Groups == PropertyGroups.None)', 'if (groups == PropertyGroups.Typography && source.Kind != NodeKind.Text)')
replace('src/VectorSpace.Workbench/StudioWorkbench.PropertyTransfer.cs', 'var dialog = Dialog("Paste properties", root, "Apply properties", "Cancel");', 'var dialog = Dialog("Paste properties", root, "Apply properties", "Cancel");\n            dialog.IsPrimaryButtonEnabled = panel.SelectedKeys.Count > 0;')
replace('server/VectorSpace.Server/Room.cs', '''            _receipts[record.Actor] = record;
        }
    }''', '''            _receipts[record.Actor] = record;
        }
        UpgradeNativeFormat();
    }''')
replace('tests/server/test_collaboration.py', "if __name__ == '__main__': unittest.main(verbosity=2)", '''    def test_schema_four_rooms_upgrade_once_without_rewriting_old_history(self):
        path, token = self.room()
        self.stop()
        metadata = Path(self.directory.name) / (path.rsplit('/', 1)[1] + '.room.json')
        data = json.loads(metadata.read_text())
        data['initial']['cells']['$root\\x1fformatVersion'] = '4'
        metadata.write_text(json.dumps(data))
        self.start()
        current = self.snapshot(path, token)
        self.assertEqual(current['cells']['$root\\x1fformatVersion'], '5')
        self.assertEqual(current['revision'], 1)
        self.assertEqual(self.request(path + '/history', token=token)[1][0]['author'], 'System')
        self.stop(); self.start()
        self.assertEqual(self.snapshot(path, token)['revision'], 1)
        batch = self.batch(self.snapshot(path, token), value='27')
        self.assertTrue(self.request(path + '/edits', batch, token)[1]['receipt']['accepted'])
        self.assertEqual(self.request(path + '/versions/0', token=token)[1]['pages'][0]['nodes'][0]['x'], 0)

if __name__ == '__main__': unittest.main(verbosity=2)''')
replace('tests/VectorSpace.Tests/EditingWorkflowTests.cs', '''        test("style cloning covers every serialized paint property",''', '''        test("compact transferred typography uses explicit constructor defaults", () => {
            var packet = PropertyClipboard.Read(PropertyClipboard.Prefix + "{\\"properties\\":{\\"typography\\":{\\"fontSize\\":19}}}");
            Check(packet.Properties.Typography!.FontSize == 19 && packet.Properties.Typography.FontFamily == "Inter" && packet.Properties.Typography.LineHeight == 1.25);
        });
        test("public rename plans validate null entries and preserve no-op history", () => {
            var n = new DesignNode(); var e = Editor(n);
            Throws<ArgumentException>(() => LayerRename.Apply(e, [null!]));
            Check(LayerRename.Apply(e, [new(n.Id, n.Name, n.Name)]) == 0 && !e.CanUndo);
        });
        test("style cloning covers every serialized paint property",''')
print('Schema migration, clipboard validation and no-op boundaries integrated.')
