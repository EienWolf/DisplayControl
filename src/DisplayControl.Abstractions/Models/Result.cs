namespace DisplayControl.Abstractions.Models
{
    public record Result(bool Success, string? Message = null)
    {
        public static Result Ok(string? message = null) => new(true, message);
        public static Result Fail(string? message = null) => new(false, message);
    }
}

