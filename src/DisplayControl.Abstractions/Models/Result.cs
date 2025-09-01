namespace DisplayControl.Abstractions.Models
{
    public record Result(
        bool Success,
        string? Message = null,
        System.Collections.Generic.IReadOnlyList<string>? Options = null,
        string? Hint = null)
    {
        public static Result Ok(
            string? message = null,
            System.Collections.Generic.IReadOnlyList<string>? options = null,
            string? hint = null) => new(true, message, options, hint);

        public static Result Fail(
            string? message = null,
            System.Collections.Generic.IReadOnlyList<string>? options = null,
            string? hint = null) => new(false, message, options, hint);
    }
}
