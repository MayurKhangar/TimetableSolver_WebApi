namespace TimetableSolver.Application.Exceptions;

public sealed class DataLoadException : Exception
{
    public string? FileName { get; }

    public DataLoadException(string message, string? fileName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        FileName = fileName;
    }
}
