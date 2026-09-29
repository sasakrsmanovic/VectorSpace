using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VectorSpace.Core;
using VectorSpace.Documents;

namespace VectorSpace.Editing;

public sealed record RenameOptions(string RenameTo, string Match = "", bool UseRegularExpression = false, int StartAt = 1);
public sealed record RenameTarget(string Id, string Name);
public sealed record LayerNameChange(string Id, string Before, string After);

/// <summary>Preflighted batch naming. Regex matching uses the linear-time non-backtracking engine;
/// unsupported lookarounds/backreferences are rejected, never run with an unbounded backtracker.</summary>
public static class LayerRename
{
    public const int MaxNameLength = 4096;
    public static IReadOnlyList<LayerNameChange> Plan(IReadOnlyList<RenameTarget> targets, RenameOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets); ArgumentNullException.ThrowIfNull(options);
        if (targets.Count > DocumentJson.MaxNodes || options.RenameTo is null || options.RenameTo.Length > MaxNameLength ||
            options.Match is null || options.Match.Length > 512 || options.StartAt is < 0 or > 999999999)
            throw new ArgumentException("Rename input exceeds the supported limits.");
        Regex? expression = null;
        if (options.Match.Length > 0)
        {
            try { expression = new(options.UseRegularExpression ? options.Match : Regex.Escape(options.Match), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(250)); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException) { throw new ArgumentException("Invalid or unsupported linear-time match expression: " + e.Message, e); }
        }
        var changes = new List<LayerNameChange>(targets.Count); var ids = new HashSet<string>(StringComparer.Ordinal); long total = 0;
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            if (target is null || string.IsNullOrEmpty(target.Id) || target.Name is null || target.Name.Length > MaxNameLength || !ids.Add(target.Id)) throw new ArgumentException("Invalid or duplicate rename target.");
            var output = new StringBuilder(Math.Min(MaxNameLength, target.Name.Length + options.RenameTo.Length));
            if (expression is null) Substitute(null);
            else
            {
                var position = 0;
                foreach (Match match in expression.Matches(target.Name))
                {
                    Append(target.Name.AsSpan(position, match.Index - position)); Substitute(match); position = match.Index + match.Length;
                }
                Append(target.Name.AsSpan(position));
            }
            var after = output.ToString();
            if (string.IsNullOrWhiteSpace(after) || after.Any(char.IsControl)) throw new ArgumentException("Layer names must contain visible text and no control characters.");
            total += after.Length;
            if (total > DocumentJson.MaxDocumentCharacters / 2) throw new ArgumentException("Batch rename exceeds the 16 MiB output budget.");
            if (after != target.Name) changes.Add(new(target.Id, target.Name, after));

            void Append(ReadOnlySpan<char> value)
            {
                if (value.Length > MaxNameLength - output.Length) throw new ArgumentException("A resulting layer name exceeds 4096 characters.");
                output.Append(value);
            }
            void Substitute(Match? match)
            {
                var template = options.RenameTo;
                for (var i = 0; i < template.Length; i++)
                {
                    if (template[i] != '$' || i + 1 == template.Length) { Append(template.AsSpan(i, 1)); continue; }
                    var token = template[++i];
                    if (token is 'n' or 'N')
                    {
                        var width = 1;
                        while (i + 1 < template.Length && template[i + 1] == token) { i++; width++; }
                        if (width > 9) throw new ArgumentException("Number padding supports up to nine digits.");
                        var number = (long)options.StartAt + (token == 'n' ? index : targets.Count - 1 - index);
                        Append(number.ToString("D" + width, CultureInfo.InvariantCulture));
                    }
                    else if (token == '$') Append("$");
                    else if (token == '&') Append(match?.Value ?? target.Name);
                    else if (token == '`') Append(match is null ? "" : target.Name.AsSpan(0, match.Index));
                    else if (token == '\'') Append(match is null ? "" : target.Name.AsSpan(match.Index + match.Length));
                    else if (char.IsAsciiDigit(token) && token != '0' && match is not null)
                    {
                        var group = token - '0';
                        while (i + 1 < template.Length && char.IsAsciiDigit(template[i + 1]))
                        { group = checked(group * 10 + template[++i] - '0'); if (group > 999) throw new ArgumentException("Capture index exceeds the supported limit."); }
                        if (group >= match.Groups.Count) throw new ArgumentException("The replacement refers to an absent capture group.");
                        Append(match.Groups[group].Value);
                    }
                    else { Append("$"); Append(template.AsSpan(i, 1)); }
                }
            }
        }
        return changes;
    }
    public static int Apply(EditorSession editor, IReadOnlyList<LayerNameChange> plan)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(plan);
        if (plan.Count == 0) return 0;
        if (plan.Count > DocumentJson.MaxNodes) throw new ArgumentException("Too many layers in the rename plan.");
        if (plan.Any(p => p is null || string.IsNullOrEmpty(p.Id) || p.Before is null || p.After is null)) throw new ArgumentException("Invalid rename plan entry.");
        if (plan.Sum(p => (long)p.After.Length) > DocumentJson.MaxDocumentCharacters / 2) throw new ArgumentException("Batch rename exceeds the output budget.");
        var ids = plan.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != plan.Count) throw new ArgumentException("Duplicate rename target.");
        var nodes = editor.Page.AllNodes().Where(n => ids.Contains(n.Id)).ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var change in plan)
        {
            if (change.After is null || change.After.Length > MaxNameLength || string.IsNullOrWhiteSpace(change.After) || change.After.Any(char.IsControl)) throw new ArgumentException("Invalid layer name.");
            if (!nodes.TryGetValue(change.Id, out var node) || node.IsEffectivelyLocked || node.Name != change.Before)
                throw new InvalidOperationException("A rename target changed, was removed or became locked. Review a fresh preview before applying.");
        }
        if (plan.All(p => p.Before == p.After)) return 0;
        editor.Edit(plan.Count == 1 ? "Rename layer" : "Rename layers", () =>
        {
            foreach (var change in plan)
            {
                var node = nodes[change.Id]; node.Name = change.After; ComponentService.SetNameOverride(node);
            }
        });
        return plan.Count;
    }
    /// <summary>Number in visual layer-panel order: front-to-back, parent before children.</summary>
    public static IReadOnlyList<RenameTarget> SelectedTargets(EditorSession editor)
    {
        var result = new List<RenameTarget>();
        foreach (var root in editor.Page.Nodes.AsEnumerable().Reverse()) Visit(root);
        return result;
        void Visit(DesignNode node)
        {
            if (editor.SelectedIds.Contains(node.Id) && !node.IsEffectivelyLocked) result.Add(new(node.Id, node.Name));
            for (var i = node.Children.Count - 1; i >= 0; i--) Visit(node.Children[i]);
        }
    }
}
