using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using IdentityServer4.EntityFramework.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Moq;
using Skoruba.AuditLogging.Services;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Mappers;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Resources;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Services;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Services.Interfaces;
using Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Entities;
using Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Repositories;
using Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Repositories.Interfaces;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.DbContexts;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using Skoruba.IdentityServer4.Admin.UnitTests.Mocks;
using Xunit;

namespace Skoruba.IdentityServer4.Admin.UnitTests.Services
{
    public class IdentityServiceTests
    {
        public IdentityServiceTests()
        {
            var databaseName = Guid.NewGuid().ToString();

            _dbContextOptions = new DbContextOptionsBuilder<AdminIdentityDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            _storeOptions = new ConfigurationStoreOptions();
            _operationalStore = new OperationalStoreOptions();
        }

        private readonly DbContextOptions<AdminIdentityDbContext> _dbContextOptions;
        private readonly ConfigurationStoreOptions _storeOptions;
        private readonly OperationalStoreOptions _operationalStore;

        private IIdentityRepository<UserIdentity, UserIdentityRole, string,
            UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
            UserIdentityUserToken> GetIdentityRepository(AdminIdentityDbContext dbContext,
            UserManager<UserIdentity> userManager,
            RoleManager<UserIdentityRole> roleManager,
            IMapper mapper)
        {
            return new IdentityRepository<AdminIdentityDbContext, UserIdentity, UserIdentityRole, string,
                UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                UserIdentityUserToken>(dbContext, userManager, roleManager, mapper);
        }

        private IIdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
            UserIdentityRole, string,
            UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
            UserIdentityUserToken,
            UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
            UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
            RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>> GetIdentityService(IIdentityRepository<UserIdentity, UserIdentityRole, string, UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim, UserIdentityUserToken> identityRepository,
            IIdentityServiceResources identityServiceResources,
            IMapper mapper, IAuditEventLogger auditEventLogger)
        {
            return new IdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
                UserIdentityRole, string,
                UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                UserIdentityUserToken,
                UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
                UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
                RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>>(identityRepository, identityServiceResources, mapper, auditEventLogger);
        }

        private IMapper GetMapper()
        {
            return new MapperConfiguration(cfg => cfg.AddProfile<IdentityMapperProfile<UserDto<string>, RoleDto<string>, UserIdentity, UserIdentityRole, string,
                        UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
                        UserIdentityUserToken,
                        UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>, UserRolesDto<RoleDto<string>, string>,
                        UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>,
                        RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>>>())
                .CreateMapper();
        }

        private UserManager<UserIdentity> GetTestUserManager(AdminIdentityDbContext context)
        {
            var testUserManager = IdentityMock.TestUserManager(new UserStore<UserIdentity, UserIdentityRole, AdminIdentityDbContext, string, UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityUserToken, UserIdentityRoleClaim>(context, new IdentityErrorDescriber()));

            return testUserManager;
        }

        private RoleManager<UserIdentityRole> GetTestRoleManager(AdminIdentityDbContext context)
        {
            var testRoleManager = IdentityMock.TestRoleManager(new RoleStore<UserIdentityRole, AdminIdentityDbContext, string, UserIdentityUserRole, UserIdentityRoleClaim>(context, new IdentityErrorDescriber()));

            return testRoleManager;
        }

        private IIdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
            UserIdentityRole, string,
            UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
            UserIdentityUserToken,
            UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>,
            UserRolesDto<RoleDto<string>, string>,
            UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
            RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>> GetIdentityService(AdminIdentityDbContext context)
        {
            var testUserManager = GetTestUserManager(context);
            var testRoleManager = GetTestRoleManager(context);
            var mapper = GetMapper();

            var identityRepository = GetIdentityRepository(context, testUserManager, testRoleManager, mapper);
            var localizerIdentityResource = new IdentityServiceResources();

            var auditLoggerMock = new Mock<IAuditEventLogger>();
            var auditLogger = auditLoggerMock.Object;

            var identityService = GetIdentityService(identityRepository, localizerIdentityResource, mapper, auditLogger);

            return identityService;
        }

        [Fact]
        public async Task AddUserAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);
            }
        }

        [Fact]
        public async Task DeleteUserProviderAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                var userProvider = IdentityMock.GenerateRandomUserProviders(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    newUserDto.Id);

                //Add new user login
                await context.UserLogins.AddAsync(userProvider);
                await context.SaveChangesAsync();

                //Get added user provider
                var addedUserProvider = await context.UserLogins.Where(x => x.ProviderKey == userProvider.ProviderKey && x.LoginProvider == userProvider.LoginProvider).SingleOrDefaultAsync();
                addedUserProvider.Should().NotBeNull();

                var userProviderDto = IdentityDtoMock<string>.GenerateRandomUserProviders(userProvider.ProviderKey, userProvider.LoginProvider,
                    userProvider.UserId);

                await identityService.DeleteUserProvidersAsync(userProviderDto);

                //Get deleted user provider
                var deletedUserProvider = await context.UserLogins.Where(x => x.ProviderKey == userProvider.ProviderKey && x.LoginProvider == userProvider.LoginProvider).SingleOrDefaultAsync();
                deletedUserProvider.Should().BeNull();
            }
        }

        [Fact]
        public async Task AddUserRoleAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                var userRoleDto = IdentityDtoMock<string>.GenerateRandomUserRole<RoleDto<string>>(roleDto.Id, userDto.Id);

                await identityService.CreateUserRoleAsync(userRoleDto);

                //Get new role
                var userRole = await context.UserRoles.Where(x => x.RoleId == roleDto.Id && x.UserId == userDto.Id).SingleOrDefaultAsync();

                userRole.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task DeleteUserRoleAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                var userRoleDto = IdentityDtoMock<string>.GenerateRandomUserRole<RoleDto<string>>(roleDto.Id, userDto.Id);

                await identityService.CreateUserRoleAsync(userRoleDto);

                //Get new role
                var userRole = await context.UserRoles.Where(x => x.RoleId == roleDto.Id && x.UserId == userDto.Id).SingleOrDefaultAsync();
                userRole.Should().NotBeNull();

                await identityService.DeleteUserRoleAsync(userRoleDto);

                //Get deleted role
                var userRoleDeleted = await context.UserRoles.Where(x => x.RoleId == roleDto.Id && x.UserId == userDto.Id).SingleOrDefaultAsync();
                userRoleDeleted.Should().BeNull();
            }
        }

        [Fact]
        public async Task AddUserClaimAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Generate random new user claim
                var userClaimDto = IdentityDtoMock<string>.GenerateRandomUserClaim(0, userDto.Id);

                await identityService.CreateUserClaimsAsync(userClaimDto);

                //Get new user claim
                var claim = await context.UserClaims.Where(x => x.ClaimType == userClaimDto.ClaimType && x.ClaimValue == userClaimDto.ClaimValue).SingleOrDefaultAsync();
                userClaimDto.ClaimId = claim.Id;

                var newUserClaim = await identityService.GetUserClaimAsync(userDto.Id.ToString(), claim.Id);

                //Assert new user claim
                userClaimDto.Should().BeEquivalentTo(newUserClaim);
            }
        }

        [Fact]
        public async Task DeleteUserClaimAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Generate random new user claim
                var userClaimDto = IdentityDtoMock<string>.GenerateRandomUserClaim(0, userDto.Id);

                await identityService.CreateUserClaimsAsync(userClaimDto);

                //Get new user claim
                var claim = await context.UserClaims.Where(x => x.ClaimType == userClaimDto.ClaimType && x.ClaimValue == userClaimDto.ClaimValue).SingleOrDefaultAsync();
                userClaimDto.ClaimId = claim.Id;

                var newUserClaim = await identityService.GetUserClaimAsync(userDto.Id.ToString(), claim.Id);

                //Assert new user claim
                userClaimDto.Should().BeEquivalentTo(newUserClaim);

                await identityService.DeleteUserClaimAsync(userClaimDto);

                //Get deleted user claim
                var deletedClaim = await context.UserClaims.Where(x => x.ClaimType == userClaimDto.ClaimType && x.ClaimValue == userClaimDto.ClaimValue).SingleOrDefaultAsync();
                deletedClaim.Should().BeNull();
            }
        }

        [Fact]
        public async Task UpdateUserAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Detached the added item
                context.Entry(user).State = EntityState.Detached;

                //Generete new user with added item id
                var userDtoForUpdate = IdentityDtoMock<string>.GenerateRandomUser(user.Id);

                //Update user
                await identityService.UpdateUserAsync(userDtoForUpdate);

                var updatedUser = await identityService.GetUserAsync(userDtoForUpdate.Id.ToString());

                //Assert updated user
                userDtoForUpdate.Should().BeEquivalentTo(updatedUser);
            }
        }

        [Fact]
        public async Task UpdateUser_WithoutEmail_DoesNotCreatePrimaryEmailRow()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                context.Entry(user).State = EntityState.Detached;

                var userDtoForUpdate = IdentityDtoMock<string>.GenerateRandomUser(user.Id);
                userDtoForUpdate.Email = null;

                await identityService.UpdateUserAsync(userDtoForUpdate);

                var primaryRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.IsPrimary).SingleOrDefaultAsync();
                primaryRow.Should().BeNull();
            }
        }

        [Fact]
        public async Task DeleteUserAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new user
                var userDto = IdentityDtoMock<string>.GenerateRandomUser();

                await identityService.CreateUserAsync(userDto);

                //Get new user
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                userDto.Id = user.Id;

                var newUserDto = await identityService.GetUserAsync(userDto.Id.ToString());

                //Assert new user
                userDto.Should().BeEquivalentTo(newUserDto);

                //Remove user
                await identityService.DeleteUserAsync(newUserDto.Id.ToString(), newUserDto);

                //Try Get Removed user
                var removeUser = await context.Users.Where(x => x.Id == user.Id)
                    .SingleOrDefaultAsync();

                //Assert removed user
                removeUser.Should().BeNull();
            }
        }

        [Fact]
        public async Task AddRoleAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);
            }
        }

        [Fact]
        public async Task UpdateRoleAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                //Detached the added item
                context.Entry(role).State = EntityState.Detached;

                //Generete new role with added item id
                var roleDtoForUpdate = IdentityDtoMock<string>.GenerateRandomRole(role.Id);

                //Update role
                await identityService.UpdateRoleAsync(roleDtoForUpdate);

                var updatedRole = await identityService.GetRoleAsync(roleDtoForUpdate.Id.ToString());

                //Assert updated role
                roleDtoForUpdate.Should().BeEquivalentTo(updatedRole);
            }
        }

        [Fact]
        public async Task DeleteRoleAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                //Remove role
                await identityService.DeleteRoleAsync(newRoleDto);

                //Try Get Removed role
                var removeRole = await context.Roles.Where(x => x.Id == role.Id)
                    .SingleOrDefaultAsync();

                //Assert removed role
                removeRole.Should().BeNull();
            }
        }

        [Fact]
        public async Task AddRoleClaimAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                //Generate random new role claim
                var roleClaimDto = IdentityDtoMock<string>.GenerateRandomRoleClaim(0, roleDto.Id);

                await identityService.CreateRoleClaimsAsync(roleClaimDto);

                //Get new role claim
                var roleClaim = await context.RoleClaims.Where(x => x.ClaimType == roleClaimDto.ClaimType && x.ClaimValue == roleClaimDto.ClaimValue).SingleOrDefaultAsync();
                roleClaimDto.ClaimId = roleClaim.Id;

                var newRoleClaimDto = await identityService.GetRoleClaimAsync(roleDto.Id.ToString(), roleClaimDto.ClaimId);

                //Assert new role
                roleClaimDto.Should().BeEquivalentTo(newRoleClaimDto, options => options.Excluding(o => o.RoleName));
            }
        }

        [Fact]
        public async Task RemoveRoleClaimAsync()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                //Generate random new role
                var roleDto = IdentityDtoMock<string>.GenerateRandomRole();

                await identityService.CreateRoleAsync(roleDto);

                //Get new role
                var role = await context.Roles.Where(x => x.Name == roleDto.Name).SingleOrDefaultAsync();
                roleDto.Id = role.Id;

                var newRoleDto = await identityService.GetRoleAsync(roleDto.Id.ToString());

                //Assert new role
                roleDto.Should().BeEquivalentTo(newRoleDto);

                //Generate random new role claim
                var roleClaimDto = IdentityDtoMock<string>.GenerateRandomRoleClaim(0, roleDto.Id);

                await identityService.CreateRoleClaimsAsync(roleClaimDto);

                //Get new role claim
                var roleClaim = await context.RoleClaims.Where(x => x.ClaimType == roleClaimDto.ClaimType && x.ClaimValue == roleClaimDto.ClaimValue).SingleOrDefaultAsync();
                roleClaimDto.ClaimId = roleClaim.Id;

                var newRoleClaimDto = await identityService.GetRoleClaimAsync(roleDto.Id.ToString(), roleClaimDto.ClaimId);

                //Assert new role
                roleClaimDto.Should().BeEquivalentTo(newRoleClaimDto, options => options.Excluding(o => o.RoleName));

                await identityService.DeleteRoleClaimAsync(roleClaimDto);

                var roleClaimToDelete = await context.RoleClaims.Where(x => x.ClaimType == roleClaimDto.ClaimType && x.ClaimValue == roleClaimDto.ClaimValue).SingleOrDefaultAsync();

                //Assert removed role claim
                roleClaimToDelete.Should().BeNull();
            }
        }

        private async Task<UserEmailAddress> AddEmailRowAsync(AdminIdentityDbContext context, string userId, string email, bool isPrimary, bool confirmed)
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

        [Fact]
        public async Task AddUserEmailAddress_FourthAddress_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "first@example.com", true, true);
                await AddEmailRowAsync(context, user.Id, "second@example.com", false, true);
                await AddEmailRowAsync(context, user.Id, "third@example.com", false, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "fourth@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("at most 3"));
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_SavedAsConfirmed()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                var originalEmail = user.Email;

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "unconfirmed@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();

                var row = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.Email == dto.Email).SingleOrDefaultAsync();
                row.Should().NotBeNull();
                row.EmailConfirmed.Should().BeTrue();

                // Regression (review #1): adding an address must not steal primary from a legacy login email.
                row.IsPrimary.Should().BeFalse();
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(originalEmail);
                var primaryRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.IsPrimary).SingleOrDefaultAsync();
                primaryRow.Should().NotBeNull();
                primaryRow.Email.Should().Be(originalEmail);
                primaryRow.EmailConfirmed.Should().BeTrue();
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_UserWithoutEmail_BecomesPrimaryAndSyncs()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                user.Email = null;
                user.NormalizedEmail = null;
                await context.SaveChangesAsync();

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "first@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();
                var row = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).SingleOrDefaultAsync();
                row.IsPrimary.Should().BeTrue();
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be("first@example.com");
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_LegacyUser_LimitCountsBootstrappedRow()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                // Legacy user (Users.Email, zero rows) gets a bootstrapped primary + 2 custom rows = cap.
                (await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = user.Id, Email = "extra1@example.com" })).Succeeded.Should().BeTrue();
                (await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = user.Id, Email = "extra2@example.com" })).Succeeded.Should().BeTrue();
                var third = await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = user.Id, Email = "extra3@example.com" });

                third.Succeeded.Should().BeFalse();
                third.Errors.Should().Contain(e => e.Description.Contains("at most 3"));
                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(3);
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_ConfirmedOnOtherAccount_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                var sharedEmail = "shared@example.com";
                await AddEmailRowAsync(context, otherUser.Id, sharedEmail, true, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = sharedEmail,
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_ConfirmedOnOtherAccountDifferentCase_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, otherUser.Id, "Shared@Example.com", false, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "shared@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_UnconfirmedOnOtherAccount_IsClearedAndSucceeds()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                var sharedEmail = "stale@example.com";
                var staleRow = await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, false);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = sharedEmail,
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();

                var otherRow = await context.Set<UserEmailAddress>().Where(x => x.Id == staleRow.Id).SingleOrDefaultAsync();
                otherRow.Should().BeNull();

                var ownRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.Email == sharedEmail).SingleOrDefaultAsync();
                ownRow.Should().NotBeNull();
                ownRow.EmailConfirmed.Should().BeTrue();
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_UnconfirmedPrimaryOnOtherAccount_FailsAndPreservesRow()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                // Victim scenario: registered but never confirmed — row is primary AND unconfirmed.
                var victimRow = await AddEmailRowAsync(context, otherUser.Id, "victim@example.com", true, false);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "victim@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));

                var preserved = await context.Set<UserEmailAddress>().Where(x => x.Id == victimRow.Id).SingleOrDefaultAsync();
                preserved.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_ConfirmedUsersEmailOnOtherAccount_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();
                otherUser.EmailConfirmed = true;
                await context.SaveChangesAsync();

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = otherUser.Email,
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_UnconfirmedUsersEmailOnOtherAccount_Succeeds()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();
                otherUser.EmailConfirmed = false;
                await context.SaveChangesAsync();

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = otherUser.Email,
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();
            }
        }

        [Fact]
        public async Task DeleteUserEmailAddress_Primary_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var primaryRow = await AddEmailRowAsync(context, user.Id, "primary@example.com", true, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    EmailAddressId = primaryRow.Id
                };

                var result = await identityService.DeleteUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("primary email address cannot be deleted"));
            }
        }

        [Fact]
        public async Task SetPrimaryUserEmailAddress_SyncsUsersEmail()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var rowA = await AddEmailRowAsync(context, user.Id, "rowa@example.com", true, true);
                var rowB = await AddEmailRowAsync(context, user.Id, "rowb@example.com", false, true);

                await identityService.SetPrimaryUserEmailAddressAsync(user.Id, rowB.Id);

                var reloadedA = await context.Set<UserEmailAddress>().Where(x => x.Id == rowA.Id).SingleOrDefaultAsync();
                var reloadedB = await context.Set<UserEmailAddress>().Where(x => x.Id == rowB.Id).SingleOrDefaultAsync();
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();

                reloadedB.IsPrimary.Should().BeTrue();
                reloadedA.IsPrimary.Should().BeFalse();
                reloadedUser.Email.Should().Be(rowB.Email);
                reloadedUser.NormalizedEmail.Should().Be(rowB.Email.ToUpperInvariant());
            }
        }

        [Fact]
        public async Task UpdateUserEmailAddress_PrimaryRow_SyncsUsersEmail()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var primaryRow = await AddEmailRowAsync(context, user.Id, "old@example.com", true, true);

                var newEmail = "new@example.com";
                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    EmailAddressId = primaryRow.Id,
                    Email = newEmail,
                    EmailConfirmed = true,
                    IsPrimary = true
                };

                var result = await identityService.UpdateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(newEmail);
            }
        }

        [Fact]
        public async Task UpdateUser_SyncsPrimaryEmailRow()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                //Detached the added item
                context.Entry(user).State = EntityState.Detached;

                var newEmail = $"{Guid.NewGuid()}@example.com";
                var userDtoForUpdate = IdentityDtoMock<string>.GenerateRandomUser(user.Id);
                userDtoForUpdate.Email = newEmail;

                await identityService.UpdateUserAsync(userDtoForUpdate);

                var primaryRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.IsPrimary).SingleOrDefaultAsync();

                primaryRow.Should().NotBeNull();
                primaryRow.Email.Should().Be(newEmail);
                primaryRow.EmailConfirmed.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GetUsers_MatchesSecondaryEmail()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "old.typo@example.com", false, true);

                var usersDto = await identityService.GetUsersAsync("old.typo");

                usersDto.Users.Should().Contain(u => u.Id == user.Id);
            }
        }
    }
}
