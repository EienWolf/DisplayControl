namespace DisplayControl.Abstractions.Models
{
    /// <summary>
    /// Represents the outcome of an operation, with optional message, options, and a hint.
    /// </summary>
    /// <param name="Success">True when the operation succeeded.</param>
    /// <param name="Message">Optional message with additional context.</param>
    /// <param name="Options">Optional list of options to help the caller decide next steps.</param>
    /// <param name="Hint">Optional hint guiding the user.</param>
    public record Result(
        bool Success,
        string? Message = null,
        System.Collections.Generic.IReadOnlyList<string>? Options = null,
        string? Hint = null)
    {
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Ok(
            string? message = null,
            System.Collections.Generic.IReadOnlyList<string>? options = null,
            string? hint = null) => new(true, message, options, hint);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static Result Fail(
            string? message = null,
            System.Collections.Generic.IReadOnlyList<string>? options = null,
            string? hint = null) => new(false, message, options, hint);
    }
}
