namespace DubbedUp.Godot.Microphone;

public sealed class RecordingException : Exception
{
    public RecordingException(string message) : base(message)
    {
    }

    public RecordingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
