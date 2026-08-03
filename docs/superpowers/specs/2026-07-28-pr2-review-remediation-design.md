# PR #2 Review Remediation — Design

**Date:** 2026-07-28
**PR:** https://github.com/StratusLIVE/IdentityServer4.Admin/pull/2 (feature/user-email-addresses)
**Review:** mattheffernan, CHANGES_REQUESTED, ran against `9a2749b`

## Context

The review raised 8 findings (3 critical, 5 important). Six commits landed after the
reviewed commit; verification against current HEAD shows:

| # | Finding | Status at HEAD | Action |
|---|---------|----------------|--------|
| 1 | First custom address overwrites legacy `Users.Email` | Valid | Fix |
| 2 | Set-primary bypasses confirmed-owner rule | Valid | Fix |
| 3 | Profile edit bypasses custom-row ownership checks | Valid | Fix |
| 4 | Conflict lookup ignores `NormalizedEmail` | Fixed in `c32e535` | PR reply |
| 5 | Reverse sync (`UpdateUserAsync`) not transactional | Valid | Fix (with #3) |
| 6 | Stale-claim cleanup commits before requested write | Fixed in `bebbcf7` | PR reply |
| 7 | Invariants vulnerable to concurrent requests | Pushback | PR reply only |
| 8 | Audit events report failures as success | Partially fixed in `757eba6` | Fix remainder |

## Decisions (made with Austin)

- **#1:** Bootstrap the legacy `Users.Email` as a confirmed primary row, then add the
  requested address as secondary. (Alternative — force new row secondary and leave no
  primary row — rejected: perpetuates the no-primary legacy state.)
- **#3:** Enforce the ownership check in `IdentityRepository.UpdateUserAsync` returning
  `IdentityResult.Failed`, not via a custom `IUserValidator`. (Validator approach touches
  DI across all four Identity variants and non-admin flows — out of scope.)
- **#7:** Push back entirely. Admin-UI email mutations are low-frequency staff operations
  already behind a relational transaction. The `UserEmailAddresses` schema is owned by
  IdentityServer's `PlatformDbContext` (this app never migrates it), so unique/filtered
  indexes cannot be added from this repo. Offer a platform-repo ticket if hard constraints
  are wanted.

## Changes

### #1 — Bootstrap legacy primary on create

`IdentityService.CreateUserEmailAddressAsync`, inside the existing
`ExecuteInTransactionAsync` block:

1. Fetch the user. If `Users.Email` is non-empty and the user has zero
   `UserEmailAddresses` rows, insert a confirmed primary row built from `Users.Email`
   (same shape `SyncPrimaryEmailRowAsync` produces) before the requested add.
2. The requested address is then added with
   `IsPrimary = currentRows.Count == 0 && string.IsNullOrEmpty(user.Email)` —
   i.e. it only becomes primary for users with no email at all.
3. The 3-row limit counts the bootstrapped row: a legacy user with an existing login
   email can add 2 custom addresses. The limit check moves inside the transaction so it
   sees the bootstrapped row.
4. `Users.Email` is never modified by an add for a user that already has an email.

Tests: regression assertions in `AddUserEmailAddress_SavedAsConfirmed` (existing email
untouched); new test for legacy-user bootstrap (row created from `Users.Email`, new
address secondary); limit interaction test.

### #2 — Set-primary revalidates ownership

`IdentityService.SetPrimaryUserEmailAddressAsync` wraps
`ResolveCrossAccountConflictAsync(userId, row.Email)` plus
`IdentityRepository.SetPrimaryUserEmailAddressAsync` in one `ExecuteInTransactionAsync`,
matching the create/update pattern. On conflict: `IdentityResult.Failed`, no promotion,
no Saved audit event.

Tests: legacy unconfirmed row cannot be promoted when another account holds the address
confirmed (via `Users` row or via confirmed custom row).

### #3 + #5 — Profile-edit path: ownership check + one transaction

`IdentityRepository.UpdateUserAsync` wraps its body in `ExecuteInTransactionAsync`:

1. If the incoming email differs from the stored one (normalized comparison), check for
   confirmed-or-primary `UserEmailAddresses` rows on other users using the normalized
   column. Conflict → `IdentityResult.Failed` with the existing conflict message; no
   `Users` update, no row sync.
2. `UserManager.UpdateAsync` and `SyncPrimaryEmailRowAsync` run inside the same
   transaction. `SyncPrimaryEmailRowAsync` becomes `Task<IdentityResult>`-returning; a
   sync failure fails (rolls back) the whole operation and is propagated to the caller.
3. 4th-row hole: when no primary row exists and the user already has 3 rows, the sync
   does not insert (skip; `Users.Email` stays authoritative). It never pushes the count
   past 3.

Tests: cross-account conflict rejected on profile edit; sync failure propagates as failed
result; no 4th row created; `EmailConfirmed` consistency between `Users` and the row.

### #8 — Audit correctness (remainder)

- Create/update/set-primary log `UserEmailAddressSavedEvent` only when the returned
  `IdentityResult.Succeeded`.
- The set-primary failure-branch event (`IdentityService.cs:609`) is removed.
- Delete logs `UserEmailAddressDeletedEvent` only on success.
- Saved events are constructed from the re-fetched persisted row, not the posted DTO.

Tests: no Saved event on failed repo write; event payload reflects persisted state.

## Testing constraints

Unit suite uses the EF InMemory provider; `ExecuteInTransactionAsync` intentionally
skips transactions there. Atomicity is therefore verified via result propagation and
state assertions, not rollback. Full suite must stay green.

## PR communication

After implementation, post one review-reply comment mapping each finding to its commit or
rationale:

- #1, #2, #3, #5, #8 → fix commits from this work.
- #4 → `c32e535`, #6 → `bebbcf7` (predate-review note: fixes landed after `9a2749b`).
- #7 → pushback rationale above, offering a platform-repo ticket for DB constraints.

## Out of scope

- Database constraints/indexes on `UserEmailAddresses` (platform repo owns the schema).
- Custom `IUserValidator` registration.
- Any change to STS/registration flows.
