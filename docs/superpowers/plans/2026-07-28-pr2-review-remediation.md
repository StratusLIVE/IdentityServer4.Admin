# PR #2 Review Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix review findings #1, #2, #3, #5, #8 on PR #2 (user email address management) per `docs/superpowers/specs/2026-07-28-pr2-review-remediation-design.md`.

**Architecture:** All changes live in two files plus tests: `IdentityService.cs` (business rules, audit) and `IdentityRepository.cs` (persistence, transactions). Every email mutation runs its checks *inside* `ExecuteInTransactionAsync` (which joins an ambient transaction when one is open, and is a pass-through on the InMemory test provider). Audit `Saved` events fire only on `IdentityResult.Succeeded` and are built from persisted rows.

**Tech Stack:** .NET (net9.0), ASP.NET Core Identity, EF Core, xUnit + FluentAssertions + Moq (InMemory provider in tests).

## Global Constraints

- The `UserEmailAddresses` table is owned by IdentityServer's `PlatformDbContext` — no migrations or schema changes in this repo.
- Max 3 `UserEmailAddresses` rows per user; at most one primary; primary row mirrors `Users.Email`.
- Existing suite (217 tests) must stay green: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
- Commit messages follow repo style: `fix: <lowercase description>` + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` trailer.
- Repository-layer error strings are plain English (existing pattern, e.g. `"Email address not found."`); service-layer messages come from `IdentityServiceResources`.

---

### Task 1: Repository plumbing — `SyncPrimaryEmailRowAsync` returns a result, add `EnsurePrimaryEmailRowAsync`

**Files:**
- Modify: `src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Repositories/IdentityRepository.cs:554-580` (sync method) and `:216` (call site)
- Modify: `src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Repositories/Interfaces/IIdentityRepository.cs:106` (add method after `ExecuteInTransactionAsync`)

**Interfaces:**
- Consumes: existing `SyncPrimaryEmailRowAsync(TUser user)`, `UserManager`, `UserEmailAddresses` DbSet.
- Produces: `Task<IdentityResult> EnsurePrimaryEmailRowAsync(string userId)` on `IIdentityRepository` (used by Task 2), and `protected virtual Task<IdentityResult> SyncPrimaryEmailRowAsync(TUser user)` with a 3-row cap guard (used by Task 4).

This task is pure plumbing verified by compilation + existing suite; new behavior is tested in Tasks 2 and 4.

- [ ] **Step 1: Change `SyncPrimaryEmailRowAsync` to return `IdentityResult` and never exceed the 3-row cap**

Replace the method at `IdentityRepository.cs:554-580` with:

```csharp
        protected virtual async Task<IdentityResult> SyncPrimaryEmailRowAsync(TUser user)
        {
            if (string.IsNullOrEmpty(user.Email)) return IdentityResult.Success;

            var userId = user.Id.ToString();
            var primary = await UserEmailAddresses.FirstOrDefaultAsync(e => e.UserId == userId && e.IsPrimary);
            if (primary == null)
            {
                // Never push a user past the 3-row cap: with 3 non-primary rows the insert is
                // skipped and Users.Email stays authoritative for login.
                var rowCount = await UserEmailAddresses.CountAsync(e => e.UserId == userId);
                if (rowCount >= 3) return IdentityResult.Success;

                await UserEmailAddresses.AddAsync(new UserEmailAddress
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Email = user.Email,
                    NormalizedEmail = UserManager.NormalizeEmail(user.Email),
                    EmailConfirmed = true,
                    IsPrimary = true
                });
                await AutoSaveChangesAsync();
            }
            else if (!string.Equals(primary.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                primary.Email = user.Email;
                primary.NormalizedEmail = UserManager.NormalizeEmail(user.Email);
                primary.EmailConfirmed = true;
                await AutoSaveChangesAsync();
            }
            return IdentityResult.Success;
        }
```

Keep the existing doc comment above the method. At the call site in `UpdateUserAsync` (`IdentityRepository.cs:216`), keep `await SyncPrimaryEmailRowAsync(user);` compiling (the returned result is consumed properly in Task 4; discarding it here is fine for this task).

- [ ] **Step 2: Add `EnsurePrimaryEmailRowAsync`**

In `IdentityRepository.cs`, directly above `SyncPrimaryEmailRowAsync`, add:

```csharp
        // Materializes a primary row from Users.Email for legacy accounts that predate the
        // UserEmailAddresses table (no-op when a primary row already exists or the user has no email).
        public virtual async Task<IdentityResult> EnsurePrimaryEmailRowAsync(string userId)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) return IdentityResult.Failed(new IdentityError { Description = "User not found." });
            return await SyncPrimaryEmailRowAsync(user);
        }
```

In `IIdentityRepository.cs`, after line 106 (`ExecuteInTransactionAsync`), add:

```csharp
        Task<IdentityResult> EnsurePrimaryEmailRowAsync(string userId);
```

- [ ] **Step 3: Build and run the full suite**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
Expected: PASS (217 tests, no behavior change yet)

- [ ] **Step 4: Commit**

```bash
git add src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity
git commit -m "refactor: sync primary email row returns a result and respects the 3-row cap; add EnsurePrimaryEmailRowAsync"
```

---

### Task 2: Finding #1 — adding the first custom address must not overwrite a legacy login email

**Files:**
- Modify: `src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Services/IdentityService.cs:527-556` (`CreateUserEmailAddressAsync`)
- Test: `tests/Skoruba.IdentityServer4.Admin.UnitTests/Services/IdentityServiceTests.cs`

**Interfaces:**
- Consumes: `IdentityRepository.EnsurePrimaryEmailRowAsync(string userId)` from Task 1; existing `GetUserAsync`, `GetUserEmailAddressesAsync`, `ExecuteInTransactionAsync`, `AddUserEmailAddressAsync`, `ResolveCrossAccountConflictAsync`.
- Produces: `CreateUserEmailAddressAsync` where the new row is primary only for users with no email and no rows; audit is gated on `result.Succeeded` and built from the persisted entity (the `conflictResult` sentinel is removed — Task 5 relies on this shape).

Note: test users created via `IdentityDtoMock<string>.GenerateRandomUser()` + `CreateUserAsync` have `Users.Email` set but no `UserEmailAddresses` rows — exactly the legacy state, so existing create tests exercise the bootstrap path.

- [ ] **Step 1: Add regression assertions to `AddUserEmailAddress_SavedAsConfirmed` and a new legacy-limit test**

In `AddUserEmailAddress_SavedAsConfirmed` (`IdentityServiceTests.cs:678`), capture `var originalEmail = user.Email;` right after fetching `user`, and replace the final assertions block (after `result.Succeeded.Should().BeTrue();`) with:

```csharp
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
```

Then add two new tests after `AddUserEmailAddress_SavedAsConfirmed`:

```csharp
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
```

- [ ] **Step 2: Run the new/changed tests to verify they fail**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.AddUserEmailAddress"`
Expected: `AddUserEmailAddress_SavedAsConfirmed` FAILS (`row.IsPrimary` is true today), `AddUserEmailAddress_UserWithoutEmail_BecomesPrimaryAndSyncs` PASSES (current behavior), `AddUserEmailAddress_LegacyUser_LimitCountsBootstrappedRow` FAILS (third add currently succeeds).

- [ ] **Step 3: Rewrite `CreateUserEmailAddressAsync`**

Replace `IdentityService.cs:527-556` with:

```csharp
        public virtual async Task<IdentityResult> CreateUserEmailAddressAsync(UserEmailAddressDto dto)
        {
            var userExists = await IdentityRepository.ExistsUserAsync(dto.UserId);
            if (!userExists) throw new UserFriendlyErrorPageException(string.Format(IdentityServiceResources.UserDoesNotExist().Description, dto.UserId), IdentityServiceResources.UserDoesNotExist().Description);

            var email = dto.Email.Trim();
            UserEmailAddress entity = null;
            var result = await IdentityRepository.ExecuteInTransactionAsync(async () =>
            {
                var user = await IdentityRepository.GetUserAsync(dto.UserId);
                var currentRows = await IdentityRepository.GetUserEmailAddressesAsync(dto.UserId);

                // Legacy accounts predate UserEmailAddresses: materialize Users.Email as the primary
                // row first so the added address can never steal primary from the login email.
                if (currentRows.Count == 0 && !string.IsNullOrEmpty(user.Email))
                {
                    var bootstrap = await IdentityRepository.EnsurePrimaryEmailRowAsync(dto.UserId);
                    if (!bootstrap.Succeeded) return bootstrap;
                    currentRows = await IdentityRepository.GetUserEmailAddressesAsync(dto.UserId);
                }

                if (currentRows.Count >= 3)
                    return IdentityResult.Failed(new IdentityError { Description = IdentityServiceResources.UserEmailAddressLimitReached().Description });

                var conflict = await ResolveCrossAccountConflictAsync(dto.UserId, email);
                if (conflict != null) return conflict;

                entity = new UserEmailAddress
                {
                    UserId = dto.UserId,
                    Email = email,
                    EmailConfirmed = true,                 // staff intervention implies verification
                    IsPrimary = currentRows.Count == 0     // only a user with no email at all gets a new primary
                };
                return await IdentityRepository.AddUserEmailAddressAsync(entity);
            });

            if (result.Succeeded)
                await AuditEventLogger.LogEventAsync(new UserEmailAddressSavedEvent(Mapper.Map<UserEmailAddressDto>(entity)));
            return result;
        }
```

(The `conflictResult` sentinel is gone: audit now keys off `result.Succeeded`, which also covers the limit and bootstrap failure paths — part of finding #8.)

- [ ] **Step 4: Run the create tests, then the full suite**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.AddUserEmailAddress"`
Expected: all PASS.
Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
Expected: PASS. If another create-path test asserts the added row is primary for a user that has an email, that assertion is asserting the bug — update it to expect the bootstrapped-primary behavior and note it in the commit message.

- [ ] **Step 5: Commit**

```bash
git add src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity tests/Skoruba.IdentityServer4.Admin.UnitTests
git commit -m "fix: bootstrap legacy Users.Email as primary row so adding an address never overwrites the login email"
```

---

### Task 3: Finding #2 — set-primary revalidates cross-account ownership

**Files:**
- Modify: `src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Services/IdentityService.cs:595-612` (`SetPrimaryUserEmailAddressAsync`)
- Test: `tests/Skoruba.IdentityServer4.Admin.UnitTests/Services/IdentityServiceTests.cs`

**Interfaces:**
- Consumes: `ResolveCrossAccountConflictAsync(string userId, string email)` (private service helper), `IdentityRepository.ExecuteInTransactionAsync`, `IdentityRepository.SetPrimaryUserEmailAddressAsync(string userId, string emailAddressId)`.
- Produces: set-primary that fails with the `UserEmailAddressConflict` message when another account owns the address; Saved audit event only on success (finding #8's failure-branch event is removed here).

- [ ] **Step 1: Write the failing tests**

Add after `SetPrimaryUserEmailAddress_SyncsUsersEmail` (`IdentityServiceTests.cs:936`):

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.SetPrimaryUserEmailAddress"`
Expected: both new tests FAIL (promotion currently succeeds); `SetPrimaryUserEmailAddress_SyncsUsersEmail` still PASSES.

- [ ] **Step 3: Rewrite `SetPrimaryUserEmailAddressAsync`**

Replace `IdentityService.cs:595-612` with:

```csharp
        public virtual async Task<IdentityResult> SetPrimaryUserEmailAddressAsync(string userId, string emailAddressId)
        {
            var row = await IdentityRepository.GetUserEmailAddressAsync(emailAddressId);
            if (row == null || row.UserId != userId)
                throw new UserFriendlyErrorPageException(string.Format(IdentityServiceResources.UserEmailAddressDoesNotExist().Description, emailAddressId), IdentityServiceResources.UserEmailAddressDoesNotExist().Description);

            var result = await IdentityRepository.ExecuteInTransactionAsync(async () =>
            {
                // Promotion confirms the row and rewrites Users.Email, so it must pass the same
                // cross-account ownership check as create/update (review #2).
                var conflict = await ResolveCrossAccountConflictAsync(userId, row.Email);
                if (conflict != null) return conflict;

                return await IdentityRepository.SetPrimaryUserEmailAddressAsync(userId, emailAddressId);
            });

            if (result.Succeeded)
            {
                var updatedRow = await IdentityRepository.GetUserEmailAddressAsync(emailAddressId);
                await AuditEventLogger.LogEventAsync(new UserEmailAddressSavedEvent(Mapper.Map<UserEmailAddressDto>(updatedRow)));
            }
            return result;
        }
```

- [ ] **Step 4: Run set-primary tests, then the full suite**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.SetPrimaryUserEmailAddress"`
Expected: all PASS.
Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity tests/Skoruba.IdentityServer4.Admin.UnitTests
git commit -m "fix: set-primary revalidates cross-account ownership inside the promotion transaction"
```

---

### Task 4: Findings #3 + #5 — profile-edit path: ownership check, one transaction, no 4th row

**Files:**
- Modify: `src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Repositories/IdentityRepository.cs:208-220` (`UpdateUserAsync`)
- Test: `tests/Skoruba.IdentityServer4.Admin.UnitTests/Services/IdentityServiceTests.cs`

**Interfaces:**
- Consumes: `GetUserEmailAddressesByEmailAsync(string email)`, `ExecuteInTransactionAsync`, `SyncPrimaryEmailRowAsync(TUser)` returning `IdentityResult` (Task 1).
- Produces: `UpdateUserAsync` that rejects emails owned (confirmed-or-primary custom row) by another user with error text containing `"already associated"`, runs `UserManager.UpdateAsync` + row sync in one transaction, and propagates the sync result. Signature unchanged: `Task<(IdentityResult identityResult, TKey userId)>`.

- [ ] **Step 1: Write the failing tests**

The existing profile-edit sync test lives around `IdentityServiceTests.cs:410-430` (asserts `primaryRow` after `UpdateUserAsync`). Add these next to the other email tests (after `AddUserEmailAddress_UnconfirmedUsersEmailOnOtherAccount_Succeeds`, `IdentityServiceTests.cs:879`):

```csharp
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

        [Fact]
        public async Task UpdateUser_UserWithThreeRowsAndNoPrimary_DoesNotCreateFourthRow()
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
                var (result, _) = await identityService.UpdateUserAsync(userDto);

                result.Succeeded.Should().BeTrue();
                (await context.Set<UserEmailAddress>().Where(x => x.UserId == user.Id).CountAsync()).Should().Be(3);
                var reloadedUser = await context.Users.Where(x => x.Id == user.Id).SingleOrDefaultAsync();
                reloadedUser.Email.Should().Be("newlogin@example.com");
            }
        }
```

Add `using Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.ExceptionHandling;` (or wherever `UserFriendlyViewException` lives — match the namespace used by `IdentityService.cs`) plus `using System;` / `using System.Threading.Tasks;` to the test file if not already present. The success-path test destructures `(IdentityResult, string)` — the service returns `Task<(IdentityResult identityResult, TKey userId)>` (`IdentityService.cs:213`).

- [ ] **Step 2: Run tests to verify expected failures**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.UpdateUser_"`
Expected: `UpdateUser_EmailConfirmedOnOtherAccountRow_Fails` FAILS (update currently succeeds). `UpdateUser_UserWithThreeRowsAndNoPrimary_DoesNotCreateFourthRow` FAILS (4th row currently created — if it passes, Task 1's cap guard already covers it; keep the test either way).

- [ ] **Step 3: Rewrite `UpdateUserAsync`**

Replace `IdentityRepository.cs:208-220` with:

```csharp
        public virtual async Task<(IdentityResult identityResult, TKey userId)> UpdateUserAsync(TUser user)
        {
            // Ownership check + Users update + primary-row sync in ONE transaction so a sync
            // failure can't leave Users.Email diverged from the primary row (review #3/#5).
            var identityResult = await ExecuteInTransactionAsync(async () =>
            {
                var userIdentity = await UserManager.FindByIdAsync(user.Id.ToString());

                if (!string.IsNullOrEmpty(user.Email))
                {
                    var normalized = UserManager.NormalizeEmail(user.Email);
                    if (!string.Equals(normalized, userIdentity.NormalizedEmail, StringComparison.Ordinal))
                    {
                        // Identity's unique-email validation only sees Users.Email; confirmed or
                        // primary custom rows on other accounts must also block the change.
                        var rows = await GetUserEmailAddressesByEmailAsync(user.Email);
                        var userId = user.Id.ToString();
                        if (rows.Any(r => r.UserId != userId && (r.EmailConfirmed || r.IsPrimary)))
                            return IdentityResult.Failed(new IdentityError { Description = $"Email {user.Email} is already associated with another account." });
                    }
                }

                Mapper.Map(user, userIdentity);
                var updateResult = await UserManager.UpdateAsync(userIdentity);
                if (!updateResult.Succeeded) return updateResult;

                return await SyncPrimaryEmailRowAsync(userIdentity);
            });

            return (identityResult, user.Id);
        }
```

Add `using System.Linq;` to the file if not already present (it is — `rows.Any` needs it).

Note the sync now receives `userIdentity` (the tracked, updated entity) instead of the incoming mapped `user` — same values post-`Mapper.Map`, but the tracked instance is the persisted source of truth.

- [ ] **Step 4: Run the new tests, then the full suite**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~IdentityServiceTests.UpdateUser_"`
Expected: PASS.
Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
Expected: PASS — pay attention to existing `UpdateUserAsync`/profile tests (~line 410) still passing.

- [ ] **Step 5: Commit**

```bash
git add src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity tests/Skoruba.IdentityServer4.Admin.UnitTests
git commit -m "fix: profile email edits enforce custom-row ownership and run atomically with the primary-row sync"
```

---

### Task 5: Finding #8 remainder — audit events only on success, built from persisted state

**Files:**
- Modify: `src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Services/IdentityService.cs:558-593` (`UpdateUserEmailAddressAsync`, `DeleteUserEmailAddressAsync`)
- Test: `tests/Skoruba.IdentityServer4.Admin.UnitTests/Services/IdentityServiceTests.cs` (plus test-helper overload at `:108-123`)

**Interfaces:**
- Consumes: create/set-primary audit gating already done in Tasks 2–3 (`result.Succeeded` + persisted-row payloads).
- Produces: same treatment for update/delete; test helper overload `GetIdentityService(AdminIdentityDbContext context, IAuditEventLogger auditEventLogger)`.

- [ ] **Step 1: Add the test-helper overload**

In `IdentityServiceTests.cs`, change the private factory at line 108 so the audit logger is injectable — replace the body of `GetIdentityService(AdminIdentityDbContext context)` and add an overload:

```csharp
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
```

- [ ] **Step 2: Write the failing audit tests**

Add after the set-primary tests:

```csharp
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
```

If `IAuditEventLogger.LogEventAsync` takes extra optional parameters in this codebase, match the Moq setup to the real signature (check `Skoruba.AuditLogging` usage; add `It.IsAny<>()` for each extra parameter). Add `using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity;` if not present.

- [ ] **Step 3: Run tests to verify expected results**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests --filter "FullyQualifiedName~LogsNoSavedEvent"`
Expected: `SetPrimaryUserEmailAddress_Conflict_LogsNoSavedEvent` PASSES already if Task 3 landed (it removed the failure-branch event); `AddUserEmailAddress_LimitReached_LogsNoSavedEvent` PASSES already if Task 2 landed. If both pass, they are regression locks — keep them and continue.

- [ ] **Step 4: Gate update/delete audit on success and use persisted state**

Replace `IdentityService.cs` `UpdateUserEmailAddressAsync` (lines 558-579) with:

```csharp
        public virtual async Task<IdentityResult> UpdateUserEmailAddressAsync(UserEmailAddressDto dto)
        {
            var row = await IdentityRepository.GetUserEmailAddressAsync(dto.EmailAddressId);
            if (row == null || row.UserId != dto.UserId)
                throw new UserFriendlyErrorPageException(string.Format(IdentityServiceResources.UserEmailAddressDoesNotExist().Description, dto.EmailAddressId), IdentityServiceResources.UserEmailAddressDoesNotExist().Description);

            var email = dto.Email.Trim();
            var result = await IdentityRepository.ExecuteInTransactionAsync(async () =>
            {
                var conflict = await ResolveCrossAccountConflictAsync(dto.UserId, email);
                if (conflict != null) return conflict;

                row.Email = email;
                row.EmailConfirmed = true;
                return await IdentityRepository.UpdateUserEmailAddressAsync(row);
            });

            if (result.Succeeded)
            {
                var persisted = await IdentityRepository.GetUserEmailAddressAsync(dto.EmailAddressId);
                await AuditEventLogger.LogEventAsync(new UserEmailAddressSavedEvent(Mapper.Map<UserEmailAddressDto>(persisted)));
            }
            return result;
        }
```

And `DeleteUserEmailAddressAsync` (lines 581-593) with:

```csharp
        public virtual async Task<IdentityResult> DeleteUserEmailAddressAsync(UserEmailAddressDto dto)
        {
            var row = await IdentityRepository.GetUserEmailAddressAsync(dto.EmailAddressId);
            if (row == null || row.UserId != dto.UserId)
                throw new UserFriendlyErrorPageException(string.Format(IdentityServiceResources.UserEmailAddressDoesNotExist().Description, dto.EmailAddressId), IdentityServiceResources.UserEmailAddressDoesNotExist().Description);

            if (row.IsPrimary)
                return IdentityResult.Failed(new IdentityError { Description = IdentityServiceResources.UserEmailAddressPrimaryDelete().Description });

            var result = await IdentityRepository.DeleteUserEmailAddressAsync(dto.EmailAddressId);
            if (result.Succeeded)
                await AuditEventLogger.LogEventAsync(new UserEmailAddressDeletedEvent(Mapper.Map<UserEmailAddressDto>(row)));
            return result;
        }
```

- [ ] **Step 5: Run the full suite**

Run: `dotnet test tests/Skoruba.IdentityServer4.Admin.UnitTests`
Expected: PASS (all tests, including the 4 findings' new tests).

- [ ] **Step 6: Commit**

```bash
git add src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity tests/Skoruba.IdentityServer4.Admin.UnitTests
git commit -m "fix: email audit events fire only on success and reflect persisted rows"
```

---

### Task 6: Push branch and post the review response

**Files:** none (git + GitHub only)

**Interfaces:**
- Consumes: all fix commits from Tasks 1–5.
- Produces: pushed branch; one PR comment mapping each finding to a commit or rationale.

- [ ] **Step 1: Push**

```bash
git push origin feature/user-email-addresses
```

- [ ] **Step 2: Draft the PR comment and get Austin's approval before posting**

Fill in the real commit SHAs from `git log --oneline`, show the draft to Austin, and only post after approval. Draft:

```markdown
All findings addressed at HEAD. Note the review ran against `9a2749b`; #4 and #6 had already been fixed in commits that landed after that point.

1. **First address overwriting legacy email** — fixed in <sha-task2>. Creating an address for a legacy user (Users.Email set, no rows) now bootstraps the login email as the confirmed primary row inside the same transaction; the added address is always secondary. Regression assertions added to `AddUserEmailAddress_SavedAsConfirmed`, plus legacy-limit and no-email tests.
2. **Set-primary ownership bypass** — fixed in <sha-task3>. Promotion now runs `ResolveCrossAccountConflictAsync` inside the promotion transaction; conflicting legacy-row tests added.
3. **Profile-edit ownership bypass** — fixed in <sha-task4>. `UpdateUserAsync` rejects emails held as confirmed-or-primary custom rows by other users (normalized comparison), and can no longer create a 4th row.
4. **NormalizedEmail conflict lookup** — was already fixed in c32e535 (with a raw-Email fallback for legacy rows with null NormalizedEmail).
5. **Non-transactional reverse sync** — fixed in <sha-task4>. The ownership check, `UserManager.UpdateAsync`, and the primary-row sync now run in one relational transaction and the sync result is propagated.
6. **Stale-claim cleanup ordering** — was already fixed in bebbcf7 (cleanup runs inside the save transaction).
7. **Concurrency/DB constraints** — pushing back on this one. These admin-UI mutations are low-frequency staff operations already serialized behind a relational transaction per request, and the `UserEmailAddresses` schema is owned by IdentityServer's `PlatformDbContext` — this repo never migrates that table, so unique/filtered indexes can't be added here. If hard constraints are wanted, that's a platform-repo change; happy to file a ticket.
8. **Audit events** — fixed in <sha-task5> (building on 757eba6). Saved/Deleted events now fire only when the returned `IdentityResult` succeeded, the set-primary failure-branch event is removed, and payloads are built from re-fetched persisted rows rather than posted DTOs.
```

- [ ] **Step 3: Post after approval**

```bash
gh pr comment 2 --repo StratusLIVE/IdentityServer4.Admin --body-file <approved-draft-file>
```
