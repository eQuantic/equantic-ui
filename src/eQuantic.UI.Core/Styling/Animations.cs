namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Animation and transition helpers with type safety.
/// Provides predefined animations and custom transition builders.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// ClassName = Animations.FadeIn;
/// ClassName = Animations.Transition("all", 300, Animations.Easing.EaseInOut);
/// </code>
/// </remarks>
public static class Animations
{
    /// <summary>
    /// Predefined CSS animations (requires @keyframes in CSS)
    /// </summary>
    public static class Presets
    {
        /// <summary>Fade in from transparent to opaque</summary>
        public static readonly EQClass FadeIn = new("eq-animate-fade-in");

        /// <summary>Fade out from opaque to transparent</summary>
        public static readonly EQClass FadeOut = new("eq-animate-fade-out");

        /// <summary>Slide in from bottom to top</summary>
        public static readonly EQClass SlideUp = new("eq-animate-slide-up");

        /// <summary>Slide in from top to bottom</summary>
        public static readonly EQClass SlideDown = new("eq-animate-slide-down");

        /// <summary>Slide in from left to right</summary>
        public static readonly EQClass SlideRight = new("eq-animate-slide-right");

        /// <summary>Slide in from right to left</summary>
        public static readonly EQClass SlideLeft = new("eq-animate-slide-left");

        /// <summary>Gentle bounce animation</summary>
        public static readonly EQClass Bounce = new("eq-animate-bounce");

        /// <summary>Pulse/heartbeat animation</summary>
        public static readonly EQClass Pulse = new("eq-animate-pulse");

        /// <summary>Spin/rotate animation</summary>
        public static readonly EQClass Spin = new("eq-animate-spin");

        /// <summary>Shake horizontally (error states)</summary>
        public static readonly EQClass Shake = new("eq-animate-shake");

        /// <summary>Scale up (zoom in)</summary>
        public static readonly EQClass ScaleIn = new("eq-animate-scale-in");

        /// <summary>Scale down (zoom out)</summary>
        public static readonly EQClass ScaleOut = new("eq-animate-scale-out");
    }

    /// <summary>
    /// Easing function constants (timing functions)
    /// </summary>
    public static class Easing
    {
        /// <summary>Smooth start and end (default, most natural)</summary>
        public const string EaseInOut = "cubic-bezier(0.4, 0, 0.2, 1)";

        /// <summary>Smooth start, sharp end</summary>
        public const string EaseIn = "cubic-bezier(0.4, 0, 1, 1)";

        /// <summary>Sharp start, smooth end (most common for exits)</summary>
        public const string EaseOut = "cubic-bezier(0, 0, 0.2, 1)";

        /// <summary>No easing (constant speed)</summary>
        public const string Linear = "linear";

        /// <summary>Sharp acceleration (for attention)</summary>
        public const string EaseInQuad = "cubic-bezier(0.55, 0.085, 0.68, 0.53)";

        /// <summary>Sharp deceleration</summary>
        public const string EaseOutQuad = "cubic-bezier(0.25, 0.46, 0.45, 0.94)";

        /// <summary>Bouncy overshoot (playful)</summary>
        public const string EaseOutBack = "cubic-bezier(0.175, 0.885, 0.32, 1.275)";
    }

    /// <summary>
    /// Duration constants in milliseconds
    /// </summary>
    public static class Duration
    {
        public const int Instant = 50;     // Almost instant
        public const int Fast = 150;       // Quick interactions (hover, active)
        public const int Base = 200;       // Standard transitions
        public const int Moderate = 300;   // More noticeable
        public const int Slow = 500;       // Deliberate animations
        public const int Slower = 700;     // Very deliberate
        public const int Slowest = 1000;   // Maximum for UI (1 second)
    }

    /// <summary>
    /// Creates a custom transition with specified properties, duration, and easing.
    /// </summary>
    /// <param name="properties">CSS properties to transition (e.g., "all", "opacity", "transform")</param>
    /// <param name="duration">Duration in milliseconds</param>
    /// <param name="easing">Easing function (use Easing constants)</param>
    /// <returns>Inline style string for transition</returns>
    /// <remarks>
    /// This returns a CSS value, not a class name. Use in inline styles:
    /// <code>
    /// Style = $"transition: {Animations.Transition("opacity", 300, Animations.Easing.EaseOut)};"
    /// </code>
    /// </remarks>
    public static string Transition(string properties, int duration, string easing = "cubic-bezier(0.4, 0, 0.2, 1)")
    {
        return $"{properties} {duration}ms {easing}";
    }

    /// <summary>
    /// Creates multiple transitions for different properties.
    /// </summary>
    /// <param name="transitions">Array of transition specs</param>
    /// <returns>Combined transition string</returns>
    /// <example>
    /// <code>
    /// var transition = Animations.TransitionMultiple(
    ///     ("opacity", 150, Easing.EaseOut),
    ///     ("transform", 300, Easing.EaseInOut)
    /// );
    /// Style = $"transition: {transition};";
    /// </code>
    /// </example>
    public static string TransitionMultiple(params (string property, int duration, string easing)[] transitions)
    {
        var parts = new List<string>();
        foreach (var (property, duration, easing) in transitions)
        {
            parts.Add(Transition(property, duration, easing));
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Animation delay helpers (for staggered animations)
    /// </summary>
    public static class Delay
    {
        public static string Ms(int milliseconds) => $"{milliseconds}ms";

        public const string Delay50 = "50ms";
        public const string Delay100 = "100ms";
        public const string Delay150 = "150ms";
        public const string Delay200 = "200ms";
        public const string Delay300 = "300ms";
        public const string Delay500 = "500ms";
    }

    /// <summary>
    /// Creates a staggered animation delay for list items.
    /// Useful for animating lists where each item appears slightly after the previous.
    /// </summary>
    /// <param name="index">Item index (0-based)</param>
    /// <param name="delayPerItem">Delay between items in milliseconds (default 50ms)</param>
    /// <returns>Animation delay value</returns>
    /// <example>
    /// <code>
    /// foreach (var (item, index) in items.Select((x, i) => (x, i)))
    /// {
    ///     Style = $"animation-delay: {Animations.StaggerDelay(index)};";
    /// }
    /// </code>
    /// </example>
    public static string StaggerDelay(int index, int delayPerItem = 50)
    {
        return $"{index * delayPerItem}ms";
    }
}
