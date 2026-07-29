using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Skoruba.AuditLogging.Services;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Mappers;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Resources;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Services;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.ExceptionHandling;
using Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Entities;
using Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Repositories;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.DbContexts;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using Skoruba.IdentityServer4.Admin.UnitTests.Mocks;
using Xunit;

namespace Skoruba.IdentityServer4.Admin.UnitTests.Services
{
    /// <summary>
    /// Tests that need a real relational provider. IdentityRepository.ExecuteInTransactionAsync
    /// short-circuits on !Database.IsRelational(), so the rest of the suite (EF InMemory) can only
    /// verify that a failed IdentityResult is propagated — never that the earlier writes in the same
    /// transaction were actually rolled back. These use Sqlite so the transaction is real.
    /// </summary>
    public class IdentityServiceRelationalTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AdminIdentityDbContext> _options;

        public IdentityServiceRelationalTests()
        {
            // Connection-scoped in-memory Sqlite: the schema lives as long as the connection is open.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AdminIdentityDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AdminIdentityDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        // Review round-2 Medium #2: prove the UpdateUserAsync transaction really rolls back.
        // Arrangement is the cap case from SyncPrimaryEmailRowAsync — three rows, no primary, none
        // matching the new address — so the sync fails AFTER UserManager.UpdateAsync has already
        // written Users.Email. Without the transaction wrapper that write would survive.
        [Fact]
        public async Task UpdateUserAsync_WhenPrimaryRowSyncFails_RollsBackUsersEmail()
        {
            string userId;
            string originalEmail;

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the rollback assertion below is vacuous on a non-relational provider");

                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;
                originalEmail = user.Email;

                await AddEmailRowAsync(context, userId, "a@example.com", false, true);
                await AddEmailRowAsync(context, userId, "b@example.com", false, true);
                await AddEmailRowAsync(context, userId, "c@example.com", false, true);

                userDto.Id = userId;
                userDto.Email = "newlogin@example.com";

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();
            }

            // Fresh context: EF does not revert the change tracker on rollback, so asserting against
            // the context that performed the write would pass without proving anything.
            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.Email.Should().Be(originalEmail, "the failed primary-row sync must roll the Users.Email write back");

                var rows = await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).ToListAsync();
                rows.Count.Should().Be(3);
                rows.Should().NotContain(r => r.IsPrimary);
            }
        }

        // Sqlite variant of PROBE_B. Worth running relationally too: this is the only place the
        // NormalizedEmail comparison is executed by a real SQL provider rather than LINQ-to-objects.
        [Fact]
        public async Task UpdateUserAsync_EmailMatchesPendingRow_PromotesInsteadOfInserting()
        {
            string userId;
            string pendingRowId;
            var pendingEmail = "pending@example.com";

            using (var context = new AdminIdentityDbContext(_options))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;

                var pendingRow = await AddEmailRowAsync(context, userId, pendingEmail, false, false);
                pendingRowId = pendingRow.Id;

                userDto.Id = userId;
                userDto.Email = pendingEmail;

                var (result, _) = await identityService.UpdateUserAsync(userDto);
                result.Succeeded.Should().BeTrue();
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                var rows = await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).ToListAsync();
                rows.Count.Should().Be(1);
                rows.Single().Id.Should().Be(pendingRowId);
                rows.Single().IsPrimary.Should().BeTrue();
                rows.Single().EmailConfirmed.Should().BeTrue();

                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.Email.Should().Be(pendingEmail);
            }
        }

        private static async Task<UserEmailAddress> AddEmailRowAsync(AdminIdentityDbContext context, string userId, string email, bool isPrimary, bool confirmed)
        {
            var row = new UserEmailAddress
            {
                Id = Guid.NewGuid().ToString(), UserId = userId, Email = email,
                NormalizedEmail = email.ToUpperInvariant(), IsPrimary = isPrimary, EmailConfirmed = confirmed
            };
            await context.Set<UserEmailAddress>().AddAsync(row);
            await context.SaveChangesAsync();
            return row;
        }

        private static IdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
            UserIdentityRole, string,
            UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
            UserIdentityUserToken,
            UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
            UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
            RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>> GetIdentityService(AdminIdentityDbContext context)
        {
            var userManager = IdentityMock.TestUserManager(new UserStore<UserIdentity, UserIdentityRole, AdminIdentityDbContext, string, UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityUserToken, UserIdentityRoleClaim>(context, new IdentityErrorDescriber()));
            var roleManager = IdentityMock.TestRoleManager(new RoleStore<UserIdentityRole, AdminIdentityDbContext, string, UserIdentityUserRole, UserIdentityRoleClaim>(context, new IdentityErrorDescriber()));

            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<IdentityMapperProfile<UserDto<string>, RoleDto<string>, UserIdentity, UserIdentityRole, string,
                    UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                    UserIdentityUserToken,
                    UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
                    UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>,
                    RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>>>())
                .CreateMapper();

            var identityRepository = new IdentityRepository<AdminIdentityDbContext, UserIdentity, UserIdentityRole, string,
                UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                UserIdentityUserToken>(context, userManager, roleManager, mapper);

            return new IdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
                UserIdentityRole, string,
                UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                UserIdentityUserToken,
                UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
                UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
                RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>>(
                identityRepository, new IdentityServiceResources(), mapper, new Mock<IAuditEventLogger>().Object);
        }
    }
}
