using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeadworksManaged.Api;
using LockOverseer.Bootstrap;
using LockOverseer.Caching;
using LockOverseer.Commands;
using LockOverseer.Config;
using LockOverseer.Contracts;
using LockOverseer.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LockOverseer;

public class LockOverseerPlugin : DeadworksPluginBase
{
    public override string Name => "LockOverseer";

    private IHost? _host;
    private CancellationTokenSource? _cts;
    private EnforcementHooks? _hooks;
    private PlaytimeTracker? _playtime;
    private ILogger<LockOverseerPlugin>? _log;
    private readonly ConcurrentDictionary<int, SlotEntry> _slotIndex = new();

    private BanCommands? _ban;
    private MuteCommands? _mute;
    private RoleCommands? _role;
    private FlagCommands? _flag;
    private InfoCommands? _info;
    private MaintenanceCommands? _maint;

    private readonly record struct SlotEntry(long SteamId, string Name);

    public override void OnLoad(bool isReload)
    {
        try
        {
            var pluginDir = Path.GetDirectoryName(typeof(LockOverseerPlugin).Assembly.Location)
                ?? Directory.GetCurrentDirectory();
            var cfg = new ConfigurationBuilder()
                .SetBasePath(pluginDir)
                .AddJsonFile("lockoverseer.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var builder = Host.CreateApplicationBuilder();
            PluginServices.AddLockOverseerCore(builder.Services, cfg, pluginDir);

            _host = builder.Build();
            _cts = new CancellationTokenSource();
            _log = _host.Services.GetRequiredService<ILogger<LockOverseerPlugin>>();

            _host.StartAsync(_cts.Token).GetAwaiter().GetResult();

            var delegatingKicker = _host.Services.GetRequiredService<LockOverseer.Bootstrap.DelegatingPlayerKicker>();
            delegatingKicker.Impl = KickBySteamId;

            _hooks = _host.Services.GetRequiredService<EnforcementHooks>();
            _playtime = _host.Services.GetRequiredService<PlaytimeTracker>();

            // Wire command classes. Constructed here (not via DI) because the
            // command-class constructors depend on delegates that close over
            // plugin state (slot lookup, reconcile trigger, Chat.PrintToChat).
            var service = _host.Services.GetRequiredService<ILockOverseerService>();
            Action<long, string> dm = SendDmTo;
            Action<long, string> kick = KickBySteamId;
            Func<Task> triggerReconcile = async () =>
            {
                if (_host is null || _cts is null) return;
                var reconcile = _host.Services.GetRequiredService<ReconcileService>();
                await reconcile.ReconcileOnceAsync(_cts.Token).ConfigureAwait(false);
            };
            Func<string> statusProvider = BuildStatus;

            var gate = new CommandGate(service, dm);
            var resolver = new PlayerResolver(GetConnectedCandidates);

            _ban   = new BanCommands(service, gate, resolver, dm);
            _mute  = new MuteCommands(service, gate, resolver, dm);
            _role  = new RoleCommands(service, gate, resolver, dm);
            _flag  = new FlagCommands(service, gate, resolver, dm);
            _info  = new InfoCommands(service, gate, resolver, dm, statusProvider);
            _maint = new MaintenanceCommands(service, gate, resolver, dm, kick, triggerReconcile);

            // Register ILockOverseerService with the Deadworks plugin service registry
            // so peer plugins can resolve the contract.
            TryRegisterWithDeadworks(service);

            // Replay crashed-session playtime and seed admins.
            _ = Task.Run(async () =>
            {
                await _playtime!.ReplayOutboxAsync(_cts.Token).ConfigureAwait(false);
                var bootstrap = _host.Services.GetRequiredService<BootstrapAdmins>();
                await bootstrap.SeedAsync(Path.Combine(pluginDir, "admins.json"), _cts.Token).ConfigureAwait(false);
            });

            // Count of [ChatCommand]-attributed methods on this plugin instance.
            int cmdCount = 0;
            foreach (var m in typeof(LockOverseerPlugin).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var _ in m.GetCustomAttributes<ChatCommandAttribute>())
                    cmdCount++;

            _log.LogInformation("[LockOverseer] {N} chat commands registered", cmdCount);
            Console.WriteLine($"[{Name}] {(isReload ? "Reloaded" : "Loaded")} ({cmdCount} chat commands)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{Name}] OnLoad failed: {ex}");
        }
    }

    public override void OnStartupServer()
    {
        // Trigger a fresh reconcile on map-load boundary.
        if (_host is null) return;
        try
        {
            var reconcile = _host.Services.GetRequiredService<ReconcileService>();
            _ = reconcile.ReconcileOnceAsync(_cts!.Token);
        }
        catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] OnStartupServer failed"); }
    }

    public override bool OnClientConnect(ClientConnectEvent args)
    {
        try
        {
            _slotIndex[args.Slot] = new SlotEntry((long)args.SteamId, args.Name ?? "");
            if (_hooks is null) return true;
            if (_hooks.ShouldRejectConnect((long)args.SteamId, out _))
                return false;

            if (_host is not null && _cts is not null)
            {
                var steamId = (long)args.SteamId;
                var name = args.Name;
                var svc = _host.Services.GetRequiredService<ILockOverseerService>();
                var client = _host.Services.GetRequiredService<LockOverseer.Api.IAuthorityClient>();
                _ = Task.Run(async () =>
                {
                    try { await client.UpsertPlayerAsync(steamId, name, _cts.Token).ConfigureAwait(false); }
                    catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] Upsert failed for {SteamId}", steamId); }
                    try { await svc.HydrateConnectedAsync(steamId, _cts.Token).ConfigureAwait(false); }
                    catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] Hydrate failed for {SteamId}", steamId); }
                });
            }
            return true;
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[LockOverseer.Authority] OnClientConnect failed");
            return true;
        }
    }

    public override void OnClientFullConnect(ClientFullConnectEvent args)
    {
        try
        {
            if (_slotIndex.TryGetValue(args.Slot, out var entry))
                _playtime?.StartSession(entry.SteamId);
        }
        catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] OnClientFullConnect failed"); }
    }

    public override void OnClientDisconnect(ClientDisconnectedEvent args)
    {
        try
        {
            if (_playtime is null || _cts is null) return;
            if (_slotIndex.TryRemove(args.Slot, out var entry))
            {
                _ = _playtime.EndSessionAsync(entry.SteamId, _cts.Token);
                _host?.Services.GetRequiredService<AuthorityCache>().ClearConnected(entry.SteamId);
            }
        }
        catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] OnClientDisconnect failed"); }
    }

    public override HookResult OnChatMessage(ChatMessage message)
    {
        try
        {
            if (_hooks is null) return HookResult.Continue;
            if (!_slotIndex.TryGetValue(message.SenderSlot, out var entry))
                return HookResult.Continue;
            if (_hooks.ShouldSuppressChat(entry.SteamId))
                return HookResult.Stop;
            return HookResult.Continue;
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "[LockOverseer.Authority] OnChatMessage failed");
            return HookResult.Continue;
        }
    }

    public override void OnUnload()
    {
        try
        {
            _cts?.Cancel();
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host?.Dispose();
        }
        catch (Exception ex) { Console.Error.WriteLine($"[{Name}] OnUnload error: {ex}"); }
        finally
        {
            _host = null;
            _cts?.Dispose();
            _cts = null;
            Console.WriteLine($"[{Name}] Unloaded");
        }
    }

    // ------------------------------------------------------------------
    // Chat command wrappers. PluginLoader.ChatCommands.cs scans this class
    // for [ChatCommand] attributes. Handler signature must be:
    //     HookResult Method(ChatCommandContext ctx)
    //
    // Command-class methods return Task, so we fire-and-forget. Errors are
    // logged via the continuation below.
    // ------------------------------------------------------------------

    private HookResult RunAsync(Func<long, Task> body, ChatCommandContext ctx)
    {
        if (!_slotIndex.TryGetValue(ctx.Message.SenderSlot, out var entry))
            return HookResult.Handled;

        long caller = entry.SteamId;
        _ = body(caller).ContinueWith(t =>
        {
            if (t.IsFaulted) _log?.LogError(t.Exception, "[LockOverseer.Authority] Chat command faulted");
        }, TaskScheduler.Default);
        return HookResult.Handled;
    }

    [ChatCommand("ban")]
    public HookResult OnBanCmd(ChatCommandContext ctx)
        => _ban is null ? HookResult.Continue : RunAsync(c => _ban.HandleBanAsync(c, ctx.Args), ctx);

    [ChatCommand("unban")]
    public HookResult OnUnbanCmd(ChatCommandContext ctx)
        => _ban is null ? HookResult.Continue : RunAsync(c => _ban.HandleUnbanAsync(c, ctx.Args), ctx);

    [ChatCommand("mute")]
    public HookResult OnMuteCmd(ChatCommandContext ctx)
        => _mute is null ? HookResult.Continue : RunAsync(c => _mute.HandleMuteAsync(c, ctx.Args), ctx);

    [ChatCommand("unmute")]
    public HookResult OnUnmuteCmd(ChatCommandContext ctx)
        => _mute is null ? HookResult.Continue : RunAsync(c => _mute.HandleUnmuteAsync(c, ctx.Args), ctx);

    [ChatCommand("kick")]
    public HookResult OnKickCmd(ChatCommandContext ctx)
        => _maint is null ? HookResult.Continue : RunAsync(c => _maint.HandleKickAsync(c, ctx.Args), ctx);

    [ChatCommand("whois")]
    public HookResult OnWhoisCmd(ChatCommandContext ctx)
        => _info is null ? HookResult.Continue : RunAsync(c => _info.HandleWhoisAsync(c, ctx.Args), ctx);

    // Multi-word commands: /role grant|revoke, /flag grant|revoke, /overseer status|reload|help.
    // Deadworks splits on spaces so "role grant" arrives here as command="role", args=["grant",...].
    [ChatCommand("role")]
    public HookResult OnRoleCmd(ChatCommandContext ctx)
    {
        if (_role is null) return HookResult.Continue;
        if (ctx.Args.Length < 1)
        {
            DmByCtx(ctx, "Usage: /role grant <player> <role> [minutes|perm]  OR  /role revoke <player>");
            return HookResult.Handled;
        }
        var sub = ctx.Args[0].ToLowerInvariant();
        var rest = SliceFrom(ctx.Args, 1);
        return sub switch
        {
            "grant"  => RunAsync(c => _role.HandleGrantAsync(c, rest), ctx),
            "revoke" => RunAsync(c => _role.HandleRevokeAsync(c, rest), ctx),
            _        => ReplyHandled(ctx, "Unknown /role subcommand. Use `grant` or `revoke`."),
        };
    }

    [ChatCommand("flag")]
    public HookResult OnFlagCmd(ChatCommandContext ctx)
    {
        if (_flag is null) return HookResult.Continue;
        if (ctx.Args.Length < 1)
        {
            DmByCtx(ctx, "Usage: /flag grant <player> <flag> [minutes|perm]  OR  /flag revoke <player> <flag>");
            return HookResult.Handled;
        }
        var sub = ctx.Args[0].ToLowerInvariant();
        var rest = SliceFrom(ctx.Args, 1);
        return sub switch
        {
            "grant"  => RunAsync(c => _flag.HandleGrantAsync(c, rest), ctx),
            "revoke" => RunAsync(c => _flag.HandleRevokeAsync(c, rest), ctx),
            _        => ReplyHandled(ctx, "Unknown /flag subcommand. Use `grant` or `revoke`."),
        };
    }

    [ChatCommand("overseer")]
    public HookResult OnOverseerCmd(ChatCommandContext ctx)
    {
        if (_info is null || _maint is null) return HookResult.Continue;
        var sub = ctx.Args.Length == 0 ? "help" : ctx.Args[0].ToLowerInvariant();
        switch (sub)
        {
            case "help":
                if (_slotIndex.TryGetValue(ctx.Message.SenderSlot, out var helpEntry))
                    _info.HandleHelp(helpEntry.SteamId);
                return HookResult.Handled;
            case "status":
                return RunAsync(c => { _info.HandleStatus(c); return Task.CompletedTask; }, ctx);
            case "reload":
                return RunAsync(c => _maint.HandleReloadAsync(c), ctx);
            default:
                return ReplyHandled(ctx, "Unknown /overseer subcommand. Use `help`, `status`, or `reload`.");
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IReadOnlyList<string> SliceFrom(string[] args, int start)
    {
        if (start >= args.Length) return Array.Empty<string>();
        var buf = new string[args.Length - start];
        Array.Copy(args, start, buf, 0, buf.Length);
        return buf;
    }

    private HookResult ReplyHandled(ChatCommandContext ctx, string text)
    {
        DmByCtx(ctx, text);
        return HookResult.Handled;
    }

    private void DmByCtx(ChatCommandContext ctx, string text)
        => Chat.PrintToChat(ctx.Message.SenderSlot, $"[LockOverseer] {text}");

    private void SendDmTo(long steamId, string text)
    {
        // Prefer the cached slot so disconnected senders can still see DMs
        // routed via delegated chat commands (there's no queue for offline DM).
        foreach (var kv in _slotIndex)
        {
            if (kv.Value.SteamId == steamId)
            {
                Chat.PrintToChat(kv.Key, $"[LockOverseer] {text}");
                return;
            }
        }
        // Fall back to server log so moderator actions never silently disappear.
        _log?.LogInformation("[LockOverseer.DM:{SteamId}] {Text}", steamId, text);
    }

    private void KickBySteamId(long steamId, string reason)
    {
        foreach (var kv in _slotIndex)
        {
            if (kv.Value.SteamId == steamId)
            {
                // Deadworks does not surface a UserId; `kickid` accepts slot.
                Server.ExecuteCommand($"kickid {kv.Key} {reason}");
                return;
            }
        }
        _log?.LogInformation("[LockOverseer.Authority] kick requested for offline {SteamId}", steamId);
    }

    private IReadOnlyList<ResolverCandidate> GetConnectedCandidates()
    {
        var list = new List<ResolverCandidate>(_slotIndex.Count);
        foreach (var kv in _slotIndex)
        {
            var name = kv.Value.Name;
            // Refresh name from the live controller when we can — names can
            // change post-connect (especially with Steam friend names).
            var ctrl = Players.FromSlot(kv.Key);
            if (ctrl is not null && !string.IsNullOrEmpty(ctrl.PlayerName))
                name = ctrl.PlayerName;
            list.Add(new ResolverCandidate(kv.Value.SteamId, kv.Key, name));
        }
        return list;
    }

    private string BuildStatus()
    {
        var sb = new StringBuilder();
        sb.Append("LockOverseer status:");
        if (_host is not null)
        {
            var reconcile = _host.Services.GetService<ReconcileService>();
            if (reconcile is not null)
            {
                sb.Append('\n').Append("  last reconcile: ")
                  .Append(reconcile.LastReconcileAt?.ToString("o") ?? "never")
                  .Append("  degraded=").Append(reconcile.IsDegraded);
            }
        }
        sb.Append('\n').Append("  connected players: ").Append(_slotIndex.Count);
        foreach (var kv in _slotIndex)
            sb.Append('\n').Append("    slot=").Append(kv.Key)
              .Append(" sid=").Append(kv.Value.SteamId)
              .Append(" name=").Append(kv.Value.Name);
        return sb.ToString();
    }

    private void TryRegisterWithDeadworks(ILockOverseerService service)
    {
        // Deadworks exposes a plugin service registry via Services.Register<T>(impl).
        // If the API surface changes, this reflection-guarded call keeps the plugin
        // loadable; peer plugins can fall back to resolving via Services.Resolve<T>().
        try
        {
            var registryType = typeof(DeadworksPluginBase).Assembly.GetType("DeadworksManaged.Api.Services");
            var register = registryType?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            register?.MakeGenericMethod(typeof(ILockOverseerService)).Invoke(null, new object[] { service });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "[LockOverseer.Authority] Could not register with Deadworks service registry; peer resolution unavailable");
        }
    }
}
