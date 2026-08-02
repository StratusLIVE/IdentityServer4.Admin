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
                // Explicit: the promoted row mirrors this checkbox, and the fixture randomizes it.
                userDto.EmailConfirmed = true;
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

        // Round-3 critical, relational variant: the profile path must DELETE another account's stale
        // unconfirmed non-primary claim, not merely allow the edit. Verified on a fresh context so it
        // proves committed state — the exact table shape IdentityServer's SingleOrDefault readers see.
        [Fact]
        public async Task UpdateUserAsync_StaleCrossAccountPendingRow_IsDeletedAndOnlyOneRowRemains()
        {
            string userId;
            string staleRowId;
            var sharedEmail = "pending@example.com";

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the committed-state assertions below are weaker on a non-relational provider");

                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = true;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.SingleAsync(x => x.UserName == otherUserDto.UserName);

                var staleRow = await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, false);
                staleRowId = staleRow.Id;

                userDto.Id = userId;
                userDto.Email = sharedEmail;

                var (result, _) = await identityService.UpdateUserAsync(userDto);
                result.Succeeded.Should().BeTrue();
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                (await verifyContext.Set<UserEmailAddress>().SingleOrDefaultAsync(x => x.Id == staleRowId))
                    .Should().BeNull("the delete must be committed, not just tracked");

                var rowsForAddress = await verifyContext.Set<UserEmailAddress>()
                    .Where(x => x.NormalizedEmail == sharedEmail.ToUpperInvariant()).ToListAsync();
                rowsForAddress.Should().HaveCount(1, "two rows for one address break EmailAddressManager.GetEmailAsync");
                rowsForAddress.Single().UserId.Should().Be(userId);
                rowsForAddress.Single().IsPrimary.Should().BeTrue();

                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.Email.Should().Be(sharedEmail);
            }
        }

        // The blocking half under a real transaction: nothing is written and nothing is deleted.
        [Fact]
        public async Task UpdateUserAsync_ConfirmedRowOnOtherAccount_RollsBackAndKeepsBothRows()
        {
            string userId;
            string originalEmail;
            string otherRowId;
            var sharedEmail = "owned@example.com";

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the rollback assertion below is vacuous on a non-relational provider");

                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;
                originalEmail = user.Email;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.SingleAsync(x => x.UserName == otherUserDto.UserName);

                var otherRow = await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, true);
                otherRowId = otherRow.Id;

                userDto.Id = userId;
                userDto.Email = sharedEmail;

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                (await verifyContext.Set<UserEmailAddress>().SingleOrDefaultAsync(x => x.Id == otherRowId)).Should().NotBeNull();

                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.Email.Should().Be(originalEmail);
                (await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).CountAsync()).Should().Be(0);
            }
        }

        // Round-3 important, relational variant: Users.EmailConfirmed and the primary row are written
        // in one transaction and must agree. Explicit false checkbox, never the randomized fixture.
        [Fact]
        public async Task UpdateUserAsync_EmailConfirmedFalse_BothStoresAgree()
        {
            string userId;
            var newEmail = "needs-verification@example.com";

            using (var context = new AdminIdentityDbContext(_options))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = true;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;

                userDto.Id = userId;
                userDto.Email = newEmail;
                userDto.EmailConfirmed = false;

                var (result, _) = await identityService.UpdateUserAsync(userDto);
                result.Succeeded.Should().BeTrue();
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.Email.Should().Be(newEmail);
                reloadedUser.EmailConfirmed.Should().BeFalse();

                var rows = await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).ToListAsync();
                rows.Should().HaveCount(1);
                rows.Single().IsPrimary.Should().BeTrue();
                rows.Single().Email.Should().Be(newEmail);
                rows.Single().EmailConfirmed.Should().Be(reloadedUser.EmailConfirmed);
                rows.Single().EmailConfirmed.Should().BeFalse();
            }
        }

        // Round-4 critical, route 1: an unrelated profile save on a legacy account (email field
        // untouched, no rows yet) reached SyncPrimaryEmailRowAsync's insert branch with no conflict
        // policy run. Relationally we can also prove the whole save is rolled back, not just refused.
        [Fact]
        public async Task UpdateUserAsync_UnrelatedSaveOnLegacyUserWhoseLoginEmailIsConfirmedElsewhere_RollsBack()
        {
            string userId;
            string otherUserId;
            string originalPhoneNumber;
            string sharedEmail;

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the rollback assertion below is vacuous on a non-relational provider");

                var identityService = GetIdentityService(context);

                // A is legacy: Users.Email set and unconfirmed, zero rows. Explicit flag, the fixture
                // randomizes it.
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = false;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;
                sharedEmail = user.Email;
                originalPhoneNumber = user.PhoneNumber;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.SingleAsync(x => x.UserName == otherUserDto.UserName);
                otherUserId = otherUser.Id;
                (await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = otherUserId, Email = sharedEmail })).Succeeded.Should().BeTrue();

                // Unrelated save: only the phone changes, so UpdateUserAsync's changed-address guard
                // is skipped and the choke-point guard is the only thing standing in the way.
                userDto.Id = userId;
                userDto.PhoneNumber = "555-0100";

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();
            }

            // Fresh context: EF does not revert the change tracker on rollback.
            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                var reloadedUser = await verifyContext.Users.SingleAsync(x => x.Id == userId);
                reloadedUser.PhoneNumber.Should().Be(originalPhoneNumber, "the refused primary-row sync must roll the whole profile save back");

                var rowsForAddress = await verifyContext.Set<UserEmailAddress>()
                    .Where(x => x.NormalizedEmail == sharedEmail.ToUpperInvariant()).ToListAsync();
                rowsForAddress.Should().HaveCount(1, "two rows for one address break EmailAddressManager.GetEmailAsync");
                rowsForAddress.Single().UserId.Should().Be(otherUserId);
                (await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).CountAsync()).Should().Be(0);
            }
        }

        // Round-4 critical, route 2: the legacy bootstrap inside CreateUserEmailAddressAsync runs
        // before any conflict check, so adding an unrelated address materialized a row for the login
        // email without ever checking it.
        [Fact]
        public async Task CreateUserEmailAddressAsync_LegacyBootstrapOfEmailConfirmedElsewhere_RollsBack()
        {
            string userId;
            string otherUserId;
            string sharedEmail;
            var addedEmail = "unrelated@example.com";

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the rollback assertion below is vacuous on a non-relational provider");

                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = false;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;
                sharedEmail = user.Email;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.SingleAsync(x => x.UserName == otherUserDto.UserName);
                otherUserId = otherUser.Id;
                (await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = otherUserId, Email = sharedEmail })).Succeeded.Should().BeTrue();

                var result = await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = userId, Email = addedEmail });

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                (await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).CountAsync())
                    .Should().Be(0, "the refused bootstrap must leave the legacy account with no rows at all");

                var rowsForAddress = await verifyContext.Set<UserEmailAddress>()
                    .Where(x => x.NormalizedEmail == sharedEmail.ToUpperInvariant()).ToListAsync();
                rowsForAddress.Should().HaveCount(1);
                rowsForAddress.Single().UserId.Should().Be(otherUserId);

                (await verifyContext.Set<UserEmailAddress>().Where(x => x.NormalizedEmail == addedEmail.ToUpperInvariant()).CountAsync()).Should().Be(0);
            }
        }

        // Round-4: relational variant of the orphan-row case. Verified on a fresh context so it proves
        // the row deletes committed alongside the user delete rather than merely being tracked.
        [Fact]
        public async Task DeleteUserAsync_RemovesOwnEmailAddressRows_AndLeavesOtherUsersRowsIntact()
        {
            string userId;
            string otherUserId;
            string survivorRowId;

            using (var context = new AdminIdentityDbContext(_options))
            {
                context.Database.IsRelational().Should().BeTrue("the committed-state assertions below are weaker on a non-relational provider");

                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.SingleAsync(x => x.UserName == userDto.UserName);
                userId = user.Id;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.SingleAsync(x => x.UserName == otherUserDto.UserName);
                otherUserId = otherUser.Id;

                await AddEmailRowAsync(context, userId, "doomed-primary@example.com", true, true);
                await AddEmailRowAsync(context, userId, "doomed-secondary@example.com", false, true);
                var survivorRow = await AddEmailRowAsync(context, otherUserId, "survivor@example.com", false, true);
                survivorRowId = survivorRow.Id;

                userDto.Id = userId;
                var result = await identityService.DeleteUserAsync(userId, userDto);
                result.Succeeded.Should().BeTrue();
            }

            using (var verifyContext = new AdminIdentityDbContext(_options))
            {
                (await verifyContext.Users.SingleOrDefaultAsync(x => x.Id == userId)).Should().BeNull();

                (await verifyContext.Set<UserEmailAddress>().Where(x => x.UserId == userId).CountAsync())
                    .Should().Be(0, "orphaned rows keep blocking their addresses after the user is gone");

                var survivor = await verifyContext.Set<UserEmailAddress>().SingleOrDefaultAsync(x => x.Id == survivorRowId);
                survivor.Should().NotBeNull();
                survivor.UserId.Should().Be(otherUserId);
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
