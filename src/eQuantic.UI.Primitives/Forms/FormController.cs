namespace eQuantic.UI.Primitives;

/// <summary>
/// A form, minus the pixels: the fields, what has happened to them, whether the whole thing is
/// valid, and the one submit that is allowed to be in flight.
/// <para>
/// It lives in <c>Primitives</c> for the reason the code editor and the spreadsheet do — a form is
/// mostly ARITHMETIC about state, and arithmetic wired into a widget can only be tested by
/// clicking. Kept here it is tested by asserting, and it runs unchanged on the server, in a
/// browser through its transpiled twin, and in a native window.
/// </para>
/// <para>
/// The submit contract is the part worth reading twice. Submitting TOUCHES every field first, so
/// the errors a user never visited become visible instead of the button silently doing nothing...
/// the most common complaint about validated forms, and it is a two-line rule.
/// </para>
/// </summary>
public sealed class FormController
{
    private readonly List<FormField> _fields = [];

    /// <summary>Raised whenever anything about the form changed — a value, a flag, an error, the
    /// submit state. A component subscribes once and calls its own SetState.</summary>
    public event Action? Changed;

    public IReadOnlyList<FormField> Fields => _fields;

    /// <summary>A submit is in flight. The button reads this for its spinner, and the controller
    /// reads it to refuse a second one.</summary>
    public bool Submitting { get; private set; }

    /// <summary>What the LAST submit failed with, when it failed for a reason no field owns — the
    /// network, a 500, a rule about the whole form. Cleared when the next submit starts.</summary>
    public string? SubmitError { get; private set; }

    /// <summary>Every rule of every field holds. Note this is about the VALUES, not about what the
    /// user has seen: a pristine form with an empty required field is invalid from the start, and
    /// simply does not say so yet.</summary>
    public bool Valid
    {
        get
        {
            // `entry`, not `field`: inside a property accessor C# 14 reads `field` as the
            // backing-field keyword, and a form library is the one place that name is unavoidable
            // everywhere else.
            foreach (var entry in _fields)
                if (entry.Error is not null) return false;
            return true;
        }
    }

    /// <summary>Any field differs from what the form opened with — the question a "discard
    /// changes?" prompt is asking.</summary>
    public bool Dirty
    {
        get
        {
            foreach (var entry in _fields)
                if (entry.Dirty) return true;
            return false;
        }
    }

    public FormField Add(string name, string initial = "", IReadOnlyList<FieldRule>? rules = null)
    {
        var field = new FormField(name, initial, rules);
        _fields.Add(field);
        return field;
    }

    /// <summary>The field by name, or null. Null rather than a throw because the caller is usually
    /// a component that would rather draw nothing than take the page down.</summary>
    public FormField? Field(string name)
    {
        foreach (var field in _fields)
            if (field.Name == name) return field;
        return null;
    }

    /// <summary>
    /// Types into a field by name and announces the change.
    /// <para>
    /// Every OTHER field revalidates too, and that is not thoroughness for its own sake: a
    /// cross-field rule is an answer about a value that just moved. Editing a password after
    /// confirming it left the confirmation reporting a match that no longer existed.
    /// </para>
    /// </summary>
    public void Set(string name, string value)
    {
        var target = Field(name);
        if (target is null) return;
        target.Set(value);
        foreach (var entry in _fields)
            if (!ReferenceEquals(entry, target)) entry.RevalidateNow();
        Changed?.Invoke();
    }

    /// <summary>The user left a field: its error becomes visible.</summary>
    public void Touch(string name)
    {
        Field(name)?.Touch();
        Changed?.Invoke();
    }

    /// <summary>
    /// Makes every error visible and answers whether the form may be submitted. Called by
    /// <see cref="SubmitAsync"/>, and callable on its own by a form that validates on a step
    /// boundary rather than on a submit.
    /// </summary>
    public bool Validate()
    {
        foreach (var field in _fields) field.Reveal();
        Changed?.Invoke();
        return Valid;
    }

    /// <summary>Back to the values the form opened with, untouched.</summary>
    public void Reset()
    {
        foreach (var field in _fields) field.Reset();
        SubmitError = null;
        Changed?.Invoke();
    }

    /// <summary>Adopts the current values as the new baseline — what a successful save does to a
    /// form the user stays on: nothing dirty, nothing shouting, nothing lost.</summary>
    public void Accept()
    {
        foreach (var field in _fields) field.Accept();
        SubmitError = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Validates, then runs the submit — which on the web is ordinarily a
    /// <c>[ServerAction]</c> awaited like any other C# method.
    /// <para>
    /// Three guarantees the caller does not have to arrange: an invalid form never reaches the
    /// handler (and every error is revealed instead), a second submit is refused while the first
    /// is in flight (the double-click that charges a card twice), and a throw becomes
    /// <see cref="SubmitError"/> rather than an unhandled exception — the network failing is a
    /// thing forms do, not a crash.
    /// </para>
    /// </summary>
    public async Task<bool> SubmitAsync(Func<Task> submit)
    {
        if (Submitting) return false;
        if (!Validate()) return false;

        Submitting = true;
        SubmitError = null;
        Changed?.Invoke();
        try
        {
            await submit();
            return true;
        }
        catch (Exception error)
        {
            SubmitError = error.Message;
            return false;
        }
        finally
        {
            Submitting = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The server's verdict, applied: a map of field name to message, plus anything that belongs
    /// to no field. Server-side validation is not a duplicate of the client's — it is the only one
    /// that counts, and this is how its answer gets back onto the form the user is looking at.
    /// </summary>
    public void ApplyServerErrors(IReadOnlyList<FieldError> errors, string? formError = null)
    {
        foreach (var error in errors) Field(error.Field)?.Fail(error.Message);
        SubmitError = formError;
        Changed?.Invoke();
    }
}

/// <summary>One server-side verdict about one field. A record so it crosses a Server Action as
/// ordinary data.</summary>
public sealed record FieldError(string Field, string Message);
