using System;
using System.Security.Cryptography;

namespace LockOverseer.Api;

public static class UuidV7
{
    public static Guid NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant 10

        return new Guid(bytes, bigEndian: true);
    }
}
