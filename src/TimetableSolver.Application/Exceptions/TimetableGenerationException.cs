namespace TimetableSolver.Application.Exceptions;

public sealed class TimetableGenerationException : Exception
{
    public TimetableGenerationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
