namespace LockOverseer.Contracts;

public interface IPlayerKicker
{
    void KickBySteamId(long steamId, string reason);
}
