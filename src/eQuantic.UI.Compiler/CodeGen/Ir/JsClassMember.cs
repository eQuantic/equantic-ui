namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The member IR — what a class is made of: fields, accessors, methods, a constructor. The
/// signature pieces stay text (a name, a parameter list, an annotation — all already final);
/// the body is a <see cref="JsStatement"/>, so the one statement writer lays it out. What the
/// member writer owns is the ASSEMBLY: modifiers, keyword, braces, and where the body goes —
/// decided once instead of in every interpolated template the emitter used to carry.
/// </summary>
public abstract record JsClassMember
{
    /// <summary>Where this member came from, when the emitter knows — what the source map records.</summary>
    public JsOrigin? Origin { get; init; }

    /// <summary>Whether the member has a body between braces — what the class layout rule keys on.</summary>
    public bool HasBody => this is JsAccessorMember or JsMethodMember or JsConstructorMember;

    /// <summary>The strangler seam: a member line as the emitter wrote it.</summary>
    public static JsClassMember Raw(string text) => new JsMemberRaw(text);

    /// <summary><c>{modifiers}{name}{annotation} = {initializer};</c> — also the declaration-only
    /// forms (<c>declare x: T;</c>, <c>abstract x: T;</c>) with no initializer.</summary>
    public static JsClassMember Field(string modifiers, string name, string annotation, string? initializer = null) =>
        new JsFieldMember(modifiers, name, annotation, initializer);

    public static JsClassMember Getter(string modifiers, string name, string annotation, JsStatement body) =>
        new JsAccessorMember(modifiers, "get", name, "", annotation, body);

    public static JsClassMember Setter(string modifiers, string name, string parameter, JsStatement body) =>
        new JsAccessorMember(modifiers, "set", name, parameter, "", body);

    public static JsClassMember Method(string modifiers, string name, string typeParameters, string parameters,
        string annotation, JsStatement body) =>
        new JsMethodMember(modifiers, name, typeParameters, parameters, annotation, body);

    public static JsClassMember Constructor(string parameters, JsStatement body) => new JsConstructorMember(parameters, body);
}

public sealed record JsMemberRaw(string Text) : JsClassMember;

public sealed record JsFieldMember(string Modifiers, string Name, string Annotation, string? Initializer) : JsClassMember;

/// <summary>A <c>get</c> or <c>set</c> accessor. <see cref="Parameters"/> is the setter's
/// <c>value</c> (with its annotation) and empty for a getter.</summary>
public sealed record JsAccessorMember(string Modifiers, string Kind, string Name, string Parameters, string Annotation,
    JsStatement Body) : JsClassMember;

public sealed record JsMethodMember(string Modifiers, string Name, string TypeParameters, string Parameters,
    string Annotation, JsStatement Body) : JsClassMember;

public sealed record JsConstructorMember(string Parameters, JsStatement Body) : JsClassMember;
