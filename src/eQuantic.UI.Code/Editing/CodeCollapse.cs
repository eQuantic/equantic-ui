namespace eQuantic.UI.Code;

/// <summary>
/// Lines <see cref="FirstLine"/> to <see cref="LastLine"/> drawn as ONE row that says how many it
/// hides, as a diff draws a long run of unchanged lines; or, with no <see cref="Placeholder"/>, as
/// no row at all, as a fold hides the lines under the header that stays.
/// </summary>
public readonly record struct CodeCollapse(int FirstLine, int LastLine, bool Placeholder = true);
