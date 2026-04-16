namespace LockOverseer.Api;

public readonly record struct SseFrame(long? Id, string Event, string Data);
