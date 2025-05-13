namespace SmoothLib;

public class SmoothException : Exception
{
    public Err ErrorCode { get; set; } = Err.InternalError;

    public SmoothException()
    {
    }

    public SmoothException(Err errorCode)
    {
        ErrorCode = errorCode;
    }

    public SmoothException(string message)
        : base(message)
    {
    }

    public SmoothException(Err errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public SmoothException(string message, Exception inner)
        : base(message, inner)
    {
    }

    public SmoothException(Err errorCode, string message, Exception inner)
    : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
