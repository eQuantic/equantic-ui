namespace eQuantic.UI.Native.Components;

/// <summary>
/// The notes a code surface drew over its code in a frame, as the hit regions see them: the surface is the
/// <paramref name="Code"/>th of the frame's code regions, and the hit regions registered for what its notes
/// hold are those from <paramref name="FirstHit"/> up to <paramref name="AfterHit"/>. A hit region registered
/// before them was painted before them, so a note covers it where they meet: a press or a pointer there is
/// the note's, as a press on a card over a button is the card's on the web, where the browser hit-tests in
/// paint order. One registered inside them is a control of the note, and one after them stands over it.
/// </summary>
internal readonly record struct CodeNoteLayer(int Code, int FirstHit, int AfterHit);
