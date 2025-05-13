namespace SmoothLib;

/// <summary>
/// Represents the error codes used in the application.
/// </summary>
public enum Err
{
    Ok = 0,
    InternalError = -1,
    NoRecord = -2,
    InvalidRequest = -3,
    InvalidArgument = -4,
    ConstraintViolation = -5,
    DatabaseError = -6,
    NonCriticalError = -7,
    InvalidConfiguration = -8,
}