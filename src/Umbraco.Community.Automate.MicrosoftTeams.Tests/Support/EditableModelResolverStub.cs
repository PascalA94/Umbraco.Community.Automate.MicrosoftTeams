using Umbraco.Automate.Core.Settings;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>An explicit resolver for tests, so they pass before and after Automate's outcomes change (spike S6).</summary>
internal sealed class EditableModelResolverStub : IEditableModelResolver
{
    public TModel ResolveModel<TModel>(string key, object? value, EditableModelSchema? schema)
        where TModel : class, new() => throw new NotSupportedException();

    public object ResolveModel(string key, Type type, object? value, EditableModelSchema? schema) => throw new NotSupportedException();
}
