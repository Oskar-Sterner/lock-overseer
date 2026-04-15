using System;

namespace LockOverseer.Contracts.Exceptions;

public class LockOverseerException : Exception
{
    public LockOverseerException(string message) : base(message) { }
    public LockOverseerException(string message, Exception inner) : base(message, inner) { }
}
