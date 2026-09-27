using System.Globalization;

namespace VectorSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private void BuildVariantInspector(DesignNode node)
    {
        if (Session.SelectionRoots.Count >= 2 && Session.SelectionRoots.All(n => n.Kind == NodeKind.Component))
            AddSection("Variants").Body.Children.Add(new StudioButton("Combine as variants", () => Run(() => ComponentVariants.Combine(Session))) { IsPrimary = true });
        if (node.Kind is not (NodeKind.Component or NodeKind.ComponentSet or NodeKind.Instance)) return;
        var set = ComponentVariants.SetFor(Session.Document, node);
        var section = AddSection(node.Kind == NodeKind.Instance ? "Component properties" : "Variants");
        if (node.Kind == NodeKind.Instance && set is not null)
        {
            var definition = Session.Document.Find(node.ComponentId)!;
            foreach (var property in set.Children.SelectMany(c => c.VariantProperties.Keys).Distinct())
            {
                var key = property;
                section.Body.Children.Add(Studio.Columns((Studio.Text(key, 11, "#9747FF"), 82), (Studio.Choice(set.Children.Select(c => c.VariantProperties.GetValueOrDefault(key, "Default")).Distinct(), definition.VariantProperties.GetValueOrDefault(key, "Default"), value => Run(() => ComponentVariants.SwitchProperty(Session, node.Id, key, value)), "Variant " + key), -1)));
            }
            section.Body.Children.Add(Wrapped(set.Name + " · " + set.Children.Count + " variants", 10));
        }
        else if (node.Kind is NodeKind.Component or NodeKind.ComponentSet)
        {
            section.Body.Children.Add(new StudioButton("Add variant", () => Run(() => ComponentVariants.Add(Session, node.Id))) { HorizontalAlignment = HorizontalAlignment.Stretch });
            if (node.Kind == NodeKind.Component && set is not null)
            {
                foreach (var pair in node.VariantProperties)
                {
                    var key = pair.Key; var input = Studio.Input(pair.Value, "Variant value " + key);
                    input.LostFocus += (_, _) => { if (Session.Document.Find(node.Id)?.VariantProperties.GetValueOrDefault(key) != input.Text) Run(() => ComponentVariants.SetProperty(Session, node.Id, key, input.Text)); };
                    section.Body.Children.Add(Studio.Columns((Studio.Text(key, 11), 82), (input, -1)));
                }
            }
            if (set is not null) section.Body.Children.Add(Wrapped("Variants are component definitions. Insert an instance from Assets, then choose its properties here.", 10));
        }
    }
    private void BuildVariableInspector(DesignNode? node)
    {
        var section = AddSection("Variables", "plus", () => RunAsync(ShowVariablesAsync));
        if (Session.Document.VariableCollections.Count == 0)
        {
            section.Body.Children.Add(new StudioButton("Create local variables", () => RunAsync(ShowVariablesAsync)) { HorizontalAlignment = HorizontalAlignment.Stretch }); return;
        }
        var modes = node?.VariableModes ?? Session.Document.VariableModes;
        foreach (var collection in Session.Document.VariableCollections)
        {
            var id = collection.Id;
            var values = new[] { (Id: "", Name: "Inherit") }.Concat(collection.Modes.Select(m => (m.Id, m.Name))).ToArray();
            section.Body.Children.Add(Studio.Columns((Studio.Text(collection.Name, 11), 82), (IdChoice(values, modes.GetValueOrDefault(id) ?? "", selected => Run(() => VariableService.SetMode(Session, id, selected.Length == 0 ? null : selected, node?.Id)), "Mode " + collection.Name), -1)));
        }
        if (node is not null)
        {
            foreach (var pair in node.VariableBindings.Where(p => !p.Value.Disabled))
            {
                var boundTarget = pair.Key; var variable = Session.Document.Variables.FirstOrDefault(v => v.Id == pair.Value.VariableId);
                section.Body.Children.Add(Studio.Columns((Studio.Text(boundTarget.ToString(), 10, Studio.Muted), 74), (Studio.Text(variable?.Name ?? "Missing", 11, "#9747FF"), -1), (new IconButton("minus", "Unbind " + boundTarget, () => Run(() => VariableService.Unbind(Session, node.Id, boundTarget))) { Width = 24 }, 24)));
            }
            var targets = Enum.GetValues<VariableTarget>().Where(t => (node.Kind == NodeKind.Text || t is not (VariableTarget.Text or VariableTarget.FontFamily or VariableTarget.FontSize or VariableTarget.LetterSpacing)) && (node.IsContainer || t is not (VariableTarget.Gap or VariableTarget.CrossGap or VariableTarget.PaddingLeft or VariableTarget.PaddingTop or VariableTarget.PaddingRight or VariableTarget.PaddingBottom))).ToArray();
            var target = targets[0]; var picker = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            void RefreshPicker() => picker.Content = IdChoice(new[] { (Id: "", Name: "Choose variable…") }.Concat(Session.Document.Variables.Where(v => v.Type == VariableResolver.TypeOf(target)).Select(v => (v.Id, v.Name))), "", id => { if (id.Length > 0) Run(() => VariableService.Bind(Session, node.Id, target, id)); }, "Bind variable");
            section.Body.Children.Add(Studio.Choice(targets.Select(t => t.ToString()), target.ToString(), value => { target = Enum.Parse<VariableTarget>(value); RefreshPicker(); }, "Variable property"));
            section.Body.Children.Add(picker); RefreshPicker();
        }
        section.Body.Children.Add(new StudioButton("Manage variables…", () => RunAsync(ShowVariablesAsync)));
    }
    private static ComboBox IdChoice(IEnumerable<(string Id, string Name)> values, string selectedId, Action<string> changed, string name)
    {
        var combo = new ComboBox { Style = (Style)StudioResources.Current["VS.ComboBox"], FontFamily = Studio.Font, Height = 30, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (id, label) in values) combo.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == selectedId);
        AutomationProperties.SetName(combo, name);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem item) changed((string)item.Tag); }; return combo;
    }
    private async Task ShowVariablesAsync()
    {
        var root = new StackPanel { Spacing = 12, Width = 450 };
        var toolbar = new StackPanel { Spacing = 8 }; root.Children.Add(toolbar);
        var rows = new StackPanel { Spacing = 10 }; var scroll = Studio.Scroll(rows); scroll.MaxHeight = 380; root.Children.Add(scroll);
        var error = Wrapped("Changes are saved with the document. Aliases keep their types; modes can be inherited or overridden per layer.", 11); root.Children.Add(error);
        var collectionId = Session.Document.VariableCollections.FirstOrDefault()?.Id;
        var modeId = Session.Document.VariableCollections.FirstOrDefault()?.DefaultModeId;
        void Execute(Action operation, bool redraw = true)
        {
            try { operation(); error.Text = "Saved locally · Undo is available after closing this window."; if (redraw) Refresh(); }
            catch (Exception ex) { error.Text = ex.Message; }
        }
        void Refresh()
        {
            toolbar.Children.Clear(); rows.Children.Clear();
            var collection = Session.Document.VariableCollections.FirstOrDefault(c => c.Id == collectionId) ?? Session.Document.VariableCollections.FirstOrDefault();
            collectionId = collection?.Id;
            var collectionName = Studio.Input("Collection " + (Session.Document.VariableCollections.Count + 1), "New collection name");
            toolbar.Children.Add(Studio.Columns((collectionName, -1), (new StudioButton("New collection", () => Execute(() => { var c = VariableService.CreateCollection(Session, collectionName.Text); collectionId = c.Id; modeId = c.DefaultModeId; })), 116)));
            if (collection is null) { rows.Children.Add(Wrapped("Create a collection, then add color, number, string, or boolean variables.")); return; }
            if (!collection.Modes.Any(m => m.Id == modeId)) modeId = collection.DefaultModeId;
            toolbar.Children.Add(Studio.Columns((IdChoice(Session.Document.VariableCollections.Select(c => (c.Id, c.Name)), collection.Id, id => { collectionId = id; modeId = null; Refresh(); }, "Variable collection"), -1), (IdChoice(collection.Modes.Select(m => (m.Id, m.Name)), modeId!, id => { modeId = id; Refresh(); }, "Editing variable mode"), -1)));
            var modeName = Studio.Input("Mode " + (collection.Modes.Count + 1), "New mode name");
            toolbar.Children.Add(Studio.Columns((modeName, -1), (new StudioButton("Add mode", () => Execute(() => modeId = VariableService.AddMode(Session, collection.Id, modeName.Text).Id)), 88), (new StudioButton("Delete mode", () => Execute(() => VariableService.RemoveMode(Session, collection.Id, modeId!))) { IsEnabled = collection.Modes.Count > 1 }, 98)));
            toolbar.Children.Add(Studio.Rule());
            var name = Studio.Input("Variable " + (Session.Document.Variables.Count + 1), "New variable name");
            var kind = VariableType.Color;
            toolbar.Children.Add(Studio.Columns((name, -1), (Studio.Choice(Enum.GetNames<VariableType>(), kind.ToString(), value => kind = Enum.Parse<VariableType>(value), "New variable type"), 104), (new StudioButton("Create", () => Execute(() => VariableService.Create(Session, collection.Id, name.Text, DefaultValue(kind)))), 66)));
            foreach (var variable in Session.Document.Variables.Where(v => v.CollectionId == collection.Id))
            {
                var id = variable.Id; var editingMode = modeId!; var value = variable.Values[editingMode];
                var row = new StackPanel { Spacing = 5 };
                var variableName = Studio.Input(variable.Name, "Variable name " + variable.Name);
                variableName.LostFocus += (_, _) => { if (Session.Document.Variables.FirstOrDefault(v => v.Id == id) is { } current && current.Name != variableName.Text && !string.IsNullOrWhiteSpace(variableName.Text)) Execute(() => Session.Edit("Rename variable", () => current.Name = variableName.Text.Trim()), false); };
                UIElement field;
                void Set(VariableValue next) => Execute(() => VariableService.SetValue(Session, id, editingMode, next), false);
                if (value.AliasId is not null) field = Studio.Text("↗ " + (Session.Document.Variables.FirstOrDefault(v => v.Id == value.AliasId)?.Name ?? "Missing"), 11, "#9747FF");
                else if (variable.Type == VariableType.Color) field = new ColorField(value.Text, color => Set(VariableValue.Color(color)));
                else if (variable.Type == VariableType.Number) field = Number("#", value.Number, number => Set(VariableValue.Float(number)), -1e7, 1e7);
                else if (variable.Type == VariableType.Boolean) field = Check("True", value.Boolean, boolean => Set(VariableValue.Bool(boolean)));
                else { var input = Studio.Input(value.Text, "Value " + variable.Name); input.LostFocus += (_, _) => { if (input.Text != value.Text) Set(VariableValue.String(input.Text)); }; field = input; }
                row.Children.Add(Studio.Columns((variableName, -1), (field, 148), (new IconButton("minus", "Delete variable " + variable.Name, () => Execute(() => VariableService.Delete(Session, id))) { Width = 24 }, 24)));
                var aliases = new[] { (Id: "", Name: "Literal value") }.Concat(Session.Document.Variables.Where(v => v.Type == variable.Type && v.Id != id).Select(v => (v.Id, "Alias · " + v.Name)));
                row.Children.Add(IdChoice(aliases, value.AliasId ?? "", alias => Execute(() =>
                {
                    var current = Session.Document.Variables.First(v => v.Id == id);
                    var next = alias.Length == 0 ? new VariableResolver(Session.Document).Resolve(id, new DesignNode { VariableModes = new() { [collection.Id] = editingMode } }) : VariableValue.Alias(Session.Document.Variables.First(v => v.Id == alias));
                    VariableService.SetValue(Session, current.Id, editingMode, next);
                }), "Alias " + variable.Name));
                row.Children.Add(Studio.Rule()); rows.Children.Add(row);
            }
        }
        Refresh(); await Dialog("Local variables", root).ShowAsync();
    }
    private static VariableValue DefaultValue(VariableType type) => type switch
    {
        VariableType.Color => VariableValue.Color("#9747FF"), VariableType.Number => VariableValue.Float(16),
        VariableType.Boolean => VariableValue.Bool(true), _ => VariableValue.String("Text")
    };
}
