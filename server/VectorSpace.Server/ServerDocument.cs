using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;

namespace VectorSpace.Server;

internal static class ServerDocument
{
    public static void Normalize(DesignDocument document)
    {
        VariableResolver.Validate(document);
        new VariableResolver(document).Apply();
        ComponentService.Synchronize(document);
        new VariableResolver(document).Apply();
        foreach (var page in document.Pages) LayoutEngine.Arrange(page.Nodes);
        DocumentJson.Validate(document);
    }
}
