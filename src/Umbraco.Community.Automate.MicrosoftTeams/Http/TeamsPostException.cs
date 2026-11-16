namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>
/// The only exception the package hands to Automate. It has no constructor that takes an inner exception: Automate
/// re-throws it into WorkflowCore, which logs the whole chain, and an inner HttpRequestException can hold the URL.
/// </summary>
public sealed class TeamsPostException(string message) : Exception(message);
