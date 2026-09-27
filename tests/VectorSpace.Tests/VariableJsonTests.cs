using System.Text.Json;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Prototyping;

internal static class VariableJsonTests
{
    public static void Register(Action<string, Action> test)
    {
        (string Json, VariableValue Expected)[] cases =
        [
            ("{\"type\":\"Boolean\",\"boolean\":true}", VariableValue.Bool(true)),
            ("{\"type\":\"Boolean\",\"boolean\":false}", VariableValue.Bool(false)),
            ("{\"type\":\"Boolean\"}", VariableValue.Bool(false)),
            ("{\"type\":\"Number\",\"number\":12.5}", VariableValue.Float(12.5)),
            ("{\"type\":\"Number\"}", VariableValue.Float(0)),
            ("{\"type\":\"String\",\"text\":\"hello\"}", VariableValue.String("hello")),
            ("{\"type\":\"String\"}", VariableValue.String("")),
            ("{\"type\":\"Color\",\"text\":\"#123456\"}", VariableValue.Color("#123456"))
        ];
        foreach (var (json, expected) in cases)
        {
            test("compact variable JSON " + json, () =>
            {
                var document = DocumentJson.Load(Envelope(json, expected.Type));
                var actual = document.Variables[0].Values["default"];
                if (actual != expected) throw new Exception($"Expected {expected}; got {JsonSerializer.Serialize(actual, VectorSpaceJsonContext.Default.VariableValue)}");
                PrototypeValidation.CheckLiteral(actual);
                if (DocumentJson.Load(DocumentJson.Save(document)).Variables[0].Values["default"] != expected)
                    throw new Exception("Literal changed after a native round trip.");
            });
        }
        foreach (var type in Enum.GetValues<VariableType>())
        {
            test("explicit null text stays invalid for " + type, () =>
            {
                var json = $$"""{"type":"{{type}}","text":null}""";
                try { DocumentJson.Load(Envelope(json, type)); }
                catch (InvalidDataException) { return; }
                throw new Exception("Explicit null text was silently normalized.");
            });
        }
        test("compact alias keeps identity and defaults", () =>
        {
            var actual = JsonSerializer.Deserialize("{\"type\":\"Boolean\",\"aliasId\":\"source\"}", VectorSpaceJsonContext.Default.VariableValue)!;
            if (actual.Text != "" || actual.Number != 0 || actual.Boolean || actual.AliasId != "source")
                throw new Exception("Compact alias defaults were lost.");
        });
        test("compact prototype conditional imports and dispatches without mutating source", () =>
        {
            var document = DocumentJson.Load(Envelope("{\"type\":\"Boolean\",\"boolean\":false}", VariableType.Boolean, """
                [{"id":"frame","kind":"Frame","prototypeFlowName":"Compact import","reactions":[{"actions":[
                  {"kind":"SetVariable","variableId":"flag","operation":"Toggle"},
                  {"kind":"Conditional","condition":{"variableId":"flag","value":{"type":"Boolean","boolean":true}},"then":[{"kind":"Navigate","targetId":"next"}]}
                ]}]},{"id":"next","kind":"Frame"}]
                """));
            var player = new PrototypeSession(document, "frame");
            if (!player.Dispatch(PrototypeTrigger.Click, "frame", userInitiated: true) || player.View.Frame.Id != "next")
                throw new Exception("Compact conditional did not navigate.");
            if (document.Variables[0].Values["default"].Boolean) throw new Exception("Playback mutated the source document.");
            player.Restart();
            if (player.Document.Variables[0].Values["default"].Boolean) throw new Exception("Restart did not reset the variable.");
        });
    }

    private static string Envelope(string value, VariableType type, string nodes = "[]") => $$"""
        {"formatVersion":3,"pages":[{"id":"page","name":"Compact values","nodes":{{nodes}}}],
         "variableCollections":[{"id":"collection","defaultModeId":"default","modes":[{"id":"default"}]}],
         "variables":[{"id":"flag","collectionId":"collection","type":"{{type}}","values":{"default":{{value}}}}]}
        """;
}
