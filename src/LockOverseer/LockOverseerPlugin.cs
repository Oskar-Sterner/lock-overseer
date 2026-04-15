using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DeadworksManaged.Api;
using LockOverseer.Bootstrap;
using LockOverseer.Caching;
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
    private readonly ConcurrentDictionary<int, long> _slotToSteamId = new();

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

            _hooks = _host.Services.GetRequiredService<EnforcementHooks>();
            _playtime = _host.Services.GetRequiredService<PlaytimeTracker>();

            // Register ILockOverseerService with the Deadworks plugin service registry
            // so peer plugins can resolve the contract.
            TryRegisterWithDeadworks(_host.Services.GetRequiredService<ILockOverseerService>());

            // Replay crashed-session playtime and seed admins.
            _ = Task.Run(async () =>
            {
                await _playtime!.ReplayOutboxAsync(_cts.Token).ConfigureAwait(false);
                var bootstrap = _host.Services.GetRequiredService<BootstrapAdmins>();
                await bootstrap.SeedAsync(Path.Combine(pluginDir, "admins.json"), _cts.Token).ConfigureAwait(false);
            });

            Console.WriteLine($"[{Name}] {(isReload ? "Reloaded" : "Loaded")}");
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
            _slotToSteamId[args.Slot] = (long)args.SteamId;
            if (_hooks is null) return true;
            if (_hooks.ShouldRejectConnect((long)args.SteamId, out _))
                return false;
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
            if (_slotToSteamId.TryGetValue(args.Slot, out var steamId))
                _playtime?.StartSession(steamId);
        }
        catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] OnClientFullConnect failed"); }
    }

    public override void OnClientDisconnect(ClientDisconnectedEvent args)
    {
        try
        {
            if (_playtime is null || _cts is null) return;
            if (_slotToSteamId.TryRemove(args.Slot, out var steamId))
                _ = _playtime.EndSessionAsync(steamId, _cts.Token);
        }
        catch (Exception ex) { _log?.LogError(ex, "[LockOverseer.Authority] OnClientDisconnect failed"); }
    }

    public override HookResult OnChatMessage(ChatMessage message)
    {
        try
        {
            if (_hooks is null) return HookResult.Continue;
            // TODO(Phase C): resolve author steam id from `message` once the command surface is in.
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
