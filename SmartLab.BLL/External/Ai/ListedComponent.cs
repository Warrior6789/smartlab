namespace SmartLab.BLL.External.Ai;

/// <summary>A component the AI looks for in a photo. The ordered quantity is left out on purpose, so it cannot just repeat it.</summary>
public sealed record ListedComponent(string Code, string Name);
