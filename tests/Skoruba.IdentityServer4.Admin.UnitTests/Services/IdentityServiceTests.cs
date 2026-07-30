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
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Mappers;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Resources;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Services;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Services.Interfaces;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.ExceptionHandling;
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
            return GetIdentityService(context, new Mock<IAuditEventLogger>().Object);
        }

        private IIdentityService<UserDto<string>, RoleDto<string>, UserIdentity,
            UserIdentityRole, string,
            UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim,
            UserIdentityUserToken,
            UsersDto<UserDto<string>, string>, RolesDto<RoleDto<string>, string>,
            UserRolesDto<RoleDto<string>, string>,
            UserClaimsDto<UserClaimDto<string>, string>, UserProviderDto<string>, UserProvidersDto<UserProviderDto<string>, string>, UserChangePasswordDto<string>,
            RoleClaimsDto<RoleClaimDto<string>, string>, UserClaimDto<string>, RoleClaimDto<string>, UserEmailDto<string>> GetIdentityService(AdminIdentityDbContext context, IAuditEventLogger auditEventLogger)
        {
            var testUserManager = GetTestUserManager(context);
            var testRoleManager = GetTestRoleManager(context);
            var mapper = GetMapper();

            var identityRepository = GetIdentityRepository(context, testUserManager, testRoleManager, mapper);
            var localizerIdentityResource = new IdentityServiceResources();

            return GetIdentityService(identityRepository, localizerIdentityResource, mapper, auditEventLogger);
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
                // Explicit: the bootstrapped primary row mirrors Users.EmailConfirmed, and the fixture
                // randomizes that flag — the assertion below would be a coin flip otherwise.
                userDto.EmailConfirmed = true;
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
        public async Task UpdateUser_EmailConfirmedOnOtherAccountRow_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                var originalEmail = user.Email;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                // Confirmed secondary custom row on the other account — invisible to Identity's
                // Users-table unique-email validation (review #3).
                var sharedEmail = "secondary-owned@example.com";
                await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, true);

                userDto.Id = user.Id;
                userDto.Email = sharedEmail;

                // Service-level UpdateUserAsync throws UserFriendlyViewException on any failed
                // IdentityResult (HandleIdentityError, IdentityService.cs:137-143).
                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(originalEmail);
            }
        }

        // Round-3 critical: the profile path used to implement only the BLOCKING half of
        // first-to-confirm. An unconfirmed non-primary row on another account passed the check and was
        // left in place, while the sync gave the edited user a primary row for the same address —
        // two rows, one address, which is what breaks EmailAddressManager.GetEmailAsync's
        // SingleOrDefault on login-by-email, confirmation and password reset.
        [Fact]
        public async Task UpdateUser_UnconfirmedNonPrimaryRowOnOtherAccount_IsClearedAndSucceeds()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = true;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                // Ordinary self-service pending row: EmailAddressManager.AddEmailAsync creates rows
                // exactly like this (unconfirmed, non-primary).
                var sharedEmail = "pending@example.com";
                var staleRow = await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, false);

                userDto.Id = user.Id;
                userDto.Email = sharedEmail;
                var (result, _) = await identityService.UpdateUserAsync(userDto);

                result.Succeeded.Should().BeTrue();

                (await context.Set<UserEmailAddress>().Where(x => x.Id == staleRow.Id).SingleOrDefaultAsync())
                    .Should().BeNull("the other account's stale claim must be deleted, not left alongside the new primary row");

                // The load-bearing assertion: IdentityServer reads this column with SingleOrDefault.
                var rowsForAddress = await context.Set<UserEmailAddress>()
                    .Where(x => x.NormalizedEmail == sharedEmail.ToUpperInvariant()).ToListAsync();
                rowsForAddress.Should().HaveCount(1);
                rowsForAddress.Single().UserId.Should().Be(user.Id);
                rowsForAddress.Single().IsPrimary.Should().BeTrue();
                rowsForAddress.Single().EmailConfirmed.Should().BeTrue();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(sharedEmail);
            }
        }

        // The other half of the same policy: an unconfirmed row that is PRIMARY is a real account's
        // login email, so it blocks and survives — deleting it would strand that user's Users.Email.
        [Fact]
        public async Task UpdateUser_UnconfirmedPrimaryRowOnOtherAccount_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                var originalEmail = user.Email;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                var victimRow = await AddEmailRowAsync(context, otherUser.Id, "victim@example.com", true, false);

                userDto.Id = user.Id;
                userDto.Email = "victim@example.com";

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();

                (await context.Set<UserEmailAddress>().Where(x => x.Id == victimRow.Id).SingleOrDefaultAsync()).Should().NotBeNull();
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(originalEmail);
            }
        }

        // Legacy account with a confirmed Users.Email and no row at all: the profile path now runs the
        // same Users-table check the address pages do.
        [Fact]
        public async Task UpdateUser_ConfirmedUsersEmailOnOtherAccount_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                var originalEmail = user.Email;

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();
                otherUser.EmailConfirmed = true;
                await context.SaveChangesAsync();

                userDto.Id = user.Id;
                userDto.Email = otherUser.Email;

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(originalEmail);
            }
        }

        // Round-3 important: Users.EmailConfirmed and the primary row must never disagree. The row
        // mirrors the profile's checkbox, so an explicitly-cleared box leaves BOTH stores unconfirmed.
        // Explicit false, never the randomized fixture value, so the mismatch cannot be masked.
        [Fact]
        public async Task UpdateUser_EmailConfirmedFalse_PrimaryRowIsUnconfirmed()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                context.Entry(user).State = EntityState.Detached;

                var newEmail = "needs-verification@example.com";
                var userDtoForUpdate = IdentityDtoMock<string>.GenerateRandomUser(user.Id);
                userDtoForUpdate.Email = newEmail;
                userDtoForUpdate.EmailConfirmed = false;

                var (result, _) = await identityService.UpdateUserAsync(userDtoForUpdate);
                result.Succeeded.Should().BeTrue();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(newEmail);
                reloadedUser.EmailConfirmed.Should().BeFalse();

                var primaryRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.IsPrimary).SingleOrDefaultAsync();
                primaryRow.Should().NotBeNull();
                primaryRow.Email.Should().Be(newEmail);
                primaryRow.EmailConfirmed.Should().Be(reloadedUser.EmailConfirmed);
                primaryRow.EmailConfirmed.Should().BeFalse();
            }
        }

        // Same rule on the promote branch, and with the address unchanged: clearing the checkbox on an
        // unrelated profile edit must not leave a confirmed row behind (and must not silently confirm
        // one either).
        [Fact]
        public async Task UpdateUser_EmailUnchangedAndConfirmedCleared_PrimaryRowFollows()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                userDto.EmailConfirmed = true;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();
                var existingEmail = user.Email;

                var primary = await AddEmailRowAsync(context, user.Id, existingEmail, true, true);

                userDto.Id = user.Id;
                userDto.Email = existingEmail;
                userDto.EmailConfirmed = false;

                var (result, _) = await identityService.UpdateUserAsync(userDto);
                result.Succeeded.Should().BeTrue();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.EmailConfirmed.Should().BeFalse();

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Should().HaveCount(1);
                rows.Single().Id.Should().Be(primary.Id);
                rows.Single().EmailConfirmed.Should().Be(reloadedUser.EmailConfirmed);
            }
        }

        // At the cap with no primary row, a Users.Email edit cannot be synced to a primary row at
        // all. Reporting success there would leave Users.Email with no matching primary row — the
        // exact drift this feature exists to prevent — so it must surface as an error.
        // (The Users.Email revert is asserted in IdentityServiceRelationalTests; the InMemory
        // provider has no transaction to roll back.)
        [Fact]
        public async Task UpdateUser_ThreeRowsNoPrimaryNoMatch_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "a@example.com", false, true);
                await AddEmailRowAsync(context, user.Id, "b@example.com", false, true);
                await AddEmailRowAsync(context, user.Id, "c@example.com", false, true);

                userDto.Id = user.Id;
                userDto.Email = "newlogin@example.com";

                Func<Task> act = () => identityService.UpdateUserAsync(userDto);
                await act.Should().ThrowAsync<UserFriendlyViewException>();

                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(3);
            }
        }

        // The at-cap case still succeeds when one of the existing rows already holds the address:
        // that row is promoted, so no fourth row is needed.
        [Fact]
        public async Task UpdateUser_ThreeRowsNoPrimaryMatchingRow_PromotesMatch()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                // Explicit: the promoted row mirrors the profile's EmailConfirmed checkbox, which the
                // fixture randomizes.
                userDto.EmailConfirmed = true;
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "a@example.com", false, true);
                var target = await AddEmailRowAsync(context, user.Id, "b@example.com", false, false);
                await AddEmailRowAsync(context, user.Id, "c@example.com", false, true);

                userDto.Id = user.Id;
                userDto.Email = "b@example.com";
                var (result, _) = await identityService.UpdateUserAsync(userDto);

                result.Succeeded.Should().BeTrue();

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Count.Should().Be(3);
                rows.Where(r => r.IsPrimary).Should().HaveCount(1);
                rows.Single(r => r.IsPrimary).Id.Should().Be(target.Id);
                rows.Single(r => r.IsPrimary).EmailConfirmed.Should().BeTrue();
            }
        }

        // Third same-user duplicate path: a primary row exists and staff edit Users.Email to an
        // address one of the user's OTHER rows already holds. Rewriting the primary's email would
        // create the duplicate; the matching row must be promoted and the old primary demoted.
        [Fact]
        public async Task UpdateUser_EmailMatchesNonPrimaryRowWhilePrimaryExists_PromotesAndDemotes()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var oldPrimary = await AddEmailRowAsync(context, user.Id, "old-primary@example.com", true, true);
                var secondary = await AddEmailRowAsync(context, user.Id, "secondary@example.com", false, false);

                userDto.Id = user.Id;
                userDto.Email = "secondary@example.com";
                var (result, _) = await identityService.UpdateUserAsync(userDto);

                result.Succeeded.Should().BeTrue();

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Count.Should().Be(2);
                rows.Where(r => r.IsPrimary).Should().HaveCount(1);
                rows.Single(r => r.IsPrimary).Id.Should().Be(secondary.Id);
                rows.Single(r => r.Id == oldPrimary.Id).Email.Should().Be("old-primary@example.com");

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be("secondary@example.com");
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
        public async Task SetPrimaryUserEmailAddress_ConfirmedOnOtherAccount_Fails()
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

                var sharedEmail = "contested@example.com";
                await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, true);   // other account confirmed it
                var primaryRow = await AddEmailRowAsync(context, user.Id, user.Email, true, true);
                var legacyRow = await AddEmailRowAsync(context, user.Id, sharedEmail, false, false); // own legacy unconfirmed row

                var result = await identityService.SetPrimaryUserEmailAddressAsync(user.Id, legacyRow.Id);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));

                var reloadedLegacy = await context.Set<UserEmailAddress>().Where(x => x.Id == legacyRow.Id).SingleOrDefaultAsync();
                reloadedLegacy.IsPrimary.Should().BeFalse();
                reloadedLegacy.EmailConfirmed.Should().BeFalse();
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(primaryRow.Email);
            }
        }

        [Fact]
        public async Task SetPrimaryUserEmailAddress_ConfirmedUsersEmailOnOtherAccount_Fails()
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

                var legacyRow = await AddEmailRowAsync(context, user.Id, otherUser.Email, false, false);

                var result = await identityService.SetPrimaryUserEmailAddressAsync(user.Id, legacyRow.Id);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated"));
            }
        }

        [Fact]
        public async Task SetPrimaryUserEmailAddress_Conflict_LogsNoSavedEvent()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var auditMock = new Mock<IAuditEventLogger>();
                var identityService = GetIdentityService(context, auditMock.Object);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var otherUserDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(otherUserDto);
                var otherUser = await context.Users.Where(x => x.UserName == otherUserDto.UserName).SingleOrDefaultAsync();

                var sharedEmail = "auditcase@example.com";
                await AddEmailRowAsync(context, otherUser.Id, sharedEmail, false, true);
                var legacyRow = await AddEmailRowAsync(context, user.Id, sharedEmail, false, false);

                var result = await identityService.SetPrimaryUserEmailAddressAsync(user.Id, legacyRow.Id);

                result.Succeeded.Should().BeFalse();
                auditMock.Verify(x => x.LogEventAsync(It.IsAny<UserEmailAddressSavedEvent>()), Times.Never);
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_LimitReached_LogsNoSavedEvent()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var auditMock = new Mock<IAuditEventLogger>();
                var identityService = GetIdentityService(context, auditMock.Object);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "first@example.com", true, true);
                await AddEmailRowAsync(context, user.Id, "second@example.com", false, true);
                await AddEmailRowAsync(context, user.Id, "third@example.com", false, true);

                var result = await identityService.CreateUserEmailAddressAsync(new UserEmailAddressDto { UserId = user.Id, Email = "fourth@example.com" });

                result.Succeeded.Should().BeFalse();
                auditMock.Verify(x => x.LogEventAsync(It.IsAny<UserEmailAddressSavedEvent>()), Times.Never);
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
                // Explicit: the primary row mirrors this checkbox, and the fixture randomizes it.
                userDtoForUpdate.EmailConfirmed = true;

                await identityService.UpdateUserAsync(userDtoForUpdate);

                var primaryRow = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id && x.IsPrimary).SingleOrDefaultAsync();

                primaryRow.Should().NotBeNull();
                primaryRow.Email.Should().Be(newEmail);
                primaryRow.EmailConfirmed.Should().BeTrue();
            }
        }

        // PROBE_A (round-2 critical): re-entering the user's existing login address on the Email
        // Addresses page must not create a second row for the same address. IdentityServer's
        // EmailAddressManager.GetEmailAsync reads this table with SingleOrDefault on the non-unique
        // Email column, so a duplicate turns login-by-email into a 500.
        [Fact]
        public async Task AddUserEmailAddress_SameAddressAsLegacyLoginEmail_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                // Legacy state: Users.Email is set but no UserEmailAddresses row exists yet.
                var loginEmail = user.Email;
                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(0);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = loginEmail,
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated with this account"));

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Count.Should().Be(1);
                rows.Single().IsPrimary.Should().BeTrue();
            }
        }

        [Fact]
        public async Task AddUserEmailAddress_SameAddressAsExistingRow_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "primary@example.com", true, true);
                await AddEmailRowAsync(context, user.Id, "secondary@example.com", false, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "secondary@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated with this account"));
                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(2);
            }
        }

        // The guard must be normalized, not a raw string compare: IdentityServer's reads rely on
        // SQL Server's case-insensitive collation, so differing case is still a duplicate row.
        [Fact]
        public async Task AddUserEmailAddress_SameAddressDifferentCase_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "primary@example.com", true, true);
                await AddEmailRowAsync(context, user.Id, "alice@example.com", false, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    Email = "Alice@Example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.CreateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated with this account"));
                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(2);
            }
        }

        [Fact]
        public async Task UpdateUserEmailAddress_ToAddressHeldByAnotherOwnRow_Fails()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var primaryRow = await AddEmailRowAsync(context, user.Id, "primary@example.com", true, true);
                var secondaryRow = await AddEmailRowAsync(context, user.Id, "secondary@example.com", false, true);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    EmailAddressId = secondaryRow.Id,
                    Email = "primary@example.com",
                    EmailConfirmed = true,
                    IsPrimary = false
                };

                var result = await identityService.UpdateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeFalse();
                result.Errors.Should().Contain(e => e.Description.Contains("already associated with this account"));

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Single(r => r.Id == primaryRow.Id).Email.Should().Be("primary@example.com");
                rows.Single(r => r.Id == secondaryRow.Id).Email.Should().Be("secondary@example.com");
            }
        }

        // Locks in the exclude-self behaviour so the duplicate guard can't regress into blocking a
        // benign re-save of a row with its own address.
        [Fact]
        public async Task UpdateUserEmailAddress_ToItsOwnCurrentAddress_Succeeds()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                await AddEmailRowAsync(context, user.Id, "primary@example.com", true, true);
                var secondaryRow = await AddEmailRowAsync(context, user.Id, "secondary@example.com", false, false);

                var dto = new UserEmailAddressDto
                {
                    UserId = user.Id,
                    EmailAddressId = secondaryRow.Id,
                    Email = "secondary@example.com",
                    EmailConfirmed = false,
                    IsPrimary = false
                };

                var result = await identityService.UpdateUserEmailAddressAsync(dto);

                result.Succeeded.Should().BeTrue();

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Count.Should().Be(2);
                rows.Single(r => r.Id == secondaryRow.Id).EmailConfirmed.Should().BeTrue();
            }
        }

        // PROBE_B (round-2 critical): EmailAddressManager.AddEmailAsync creates rows as
        // IsPrimary=false/EmailConfirmed=false, so "pending self-service row, no primary row" is
        // ordinary production state. Editing Users.Email to that address must promote the existing
        // row, not insert a second one.
        [Fact]
        public async Task UpdateUser_EmailMatchesPendingRowWithNoPrimary_PromotesInsteadOfInserting()
        {
            using (var context = new AdminIdentityDbContext(_dbContextOptions))
            {
                var identityService = GetIdentityService(context);

                var userDto = IdentityDtoMock<string>.GenerateRandomUser();
                await identityService.CreateUserAsync(userDto);
                var user = await context.Users.Where(x => x.UserName == userDto.UserName).SingleOrDefaultAsync();

                var pendingEmail = "pending@example.com";
                var pendingRow = await AddEmailRowAsync(context, user.Id, pendingEmail, false, false);

                context.Entry(user).State = EntityState.Detached;

                var userDtoForUpdate = IdentityDtoMock<string>.GenerateRandomUser(user.Id);
                userDtoForUpdate.Email = pendingEmail;
                // Explicit: the promoted row mirrors this checkbox, and the fixture randomizes it.
                userDtoForUpdate.EmailConfirmed = true;

                var (result, _) = await identityService.UpdateUserAsync(userDtoForUpdate);

                result.Succeeded.Should().BeTrue();

                var rows = await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).ToListAsync();
                rows.Count.Should().Be(1);
                rows.Single().Id.Should().Be(pendingRow.Id);
                rows.Single().IsPrimary.Should().BeTrue();
                rows.Single().EmailConfirmed.Should().BeTrue();

                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be(pendingEmail);
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
