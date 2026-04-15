using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LockOverseer.Api;
using LockOverseer.Api.Dto;
using LockOverseer.Config;
using LockOverseer.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace LockOverseer.UnitTests.Config;

public sealed class BootstrapAdminsTests
{
    private static IAuthorityClient BuildClient(RoleResource[] existingRoles)
    {
        var client = Substitute.For<IAuthorityClient>();
        client.GetRolesAsync(Arg.Any<CancellationToken>())
              .Returns(Result<System.Collections.Generic.IReadOnlyList<RoleResource>>.Ok(existingRoles));
        client.CreateRoleAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(),
                               Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
              .Returns(ci => Result<RoleResource>.Ok(
                  new RoleResource(ci.ArgAt<string>(0), ci.ArgAt<string?>(1), ci.ArgAt<int>(2),
                                   ci.ArgAt<System.Collections.Generic.IReadOnlyList<string>>(3))));
        return client;
    }

    [Fact]
    public async Task Seeds_admin_role_when_no_active_admin_assignments_exist()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, """[ {"steam_id": 76561198000000001, "label":"oskar"} ]""");

        var client = BuildClient(System.Array.Empty<RoleResource>());
        client.GetPlayerRolesAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
              .Returns(Result<System.Collections.Generic.IReadOnlyList<RoleAssignmentResource>>.Ok(
                  System.Array.Empty<RoleAssignmentResource>()));
        client.GrantRoleAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<int?>(),
                              Arg.Any<IssuerResource>(), Arg.Any<CancellationToken>())
              .Returns(Result<RoleAssignmentResource>.Ok(new RoleAssignmentResource(
                  1, 76561198000000001, "admin", System.DateTimeOffset.UnixEpoch, null, null,
                  AssignedBySteamId: null, AssignedByLabel: "bootstrap")));

        var sut = new BootstrapAdmins(client, NullLogger<BootstrapAdmins>.Instance);
        await sut.SeedAsync(tmp, CancellationToken.None);

        await client.Received(1).GrantRoleAsync(76561198000000001, "admin", null,
            Arg.Is<IssuerResource>(i => i.Label == "bootstrap"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_seeding_when_any_active_admin_assignment_exists()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, """[ {"steam_id": 1, "label":"a"} ]""");

        var client = BuildClient(new[]
        {
            new RoleResource("admin", null, 100, System.Array.Empty<string>()),
            new RoleResource("mod", null, 50, System.Array.Empty<string>()),
            new RoleResource("player", null, 0, System.Array.Empty<string>()),
        });
        client.GetPlayerRolesAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
              .Returns(Result<System.Collections.Generic.IReadOnlyList<RoleAssignmentResource>>.Ok(new[]
              {
                  new RoleAssignmentResource(1, 1, "admin", System.DateTimeOffset.UnixEpoch, null, null,
                      AssignedBySteamId: null, AssignedByLabel: "chat")
              }));

        var sut = new BootstrapAdmins(client, NullLogger<BootstrapAdmins>.Instance);
        await sut.SeedAsync(tmp, CancellationToken.None);

        await client.DidNotReceiveWithAnyArgs().GrantRoleAsync(default, default!, default, default!, default);
    }

    [Fact]
    public async Task SeedRoles_creates_missing_bootstrap_roles_and_skips_existing()
    {
        var client = BuildClient(new[]
        {
            new RoleResource("admin", "existing", 100, new[] { "overseer.admin" }),
        });

        var sut = new BootstrapAdmins(client, NullLogger<BootstrapAdmins>.Instance);
        await sut.SeedRolesAsync(CancellationToken.None);

        await client.DidNotReceive().CreateRoleAsync("admin", Arg.Any<string?>(), Arg.Any<int>(),
            Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await client.Received(1).CreateRoleAsync("mod", Arg.Any<string?>(), 50,
            Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await client.Received(1).CreateRoleAsync("player", Arg.Any<string?>(), 0,
            Arg.Any<System.Collections.Generic.IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }
}
