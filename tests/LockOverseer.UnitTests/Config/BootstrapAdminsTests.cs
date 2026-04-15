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
    [Fact]
    public async Task Seeds_admin_role_when_no_active_admin_assignments_exist()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, """[ {"steam_id": 76561198000000001, "label":"oskar"} ]""");

        var client = Substitute.For<IAuthorityClient>();
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

        var client = Substitute.For<IAuthorityClient>();
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
}
