# ADR-20261102-156: Staged Recovery Kit Activation

## Status
Accepted

## Date
2026-11-02

## Context
Users need to retrieve their existing recovery material after recovery login, and password-backed users need a safe way to issue a replacement kit. Replacing the active key before the user has safely saved the new material risks account lockout. A recovery JSON file contains credential material and must not be described as encrypted.

## Decision
1. Reuse a shared recovery-kit modal to display recovery material returned by recovery login and to guide explicit confirmation of a newly issued kit.
2. Provide authenticated generation and activation endpoints. Resolve account identity from authenticated claims, require current-password verification for generation, reject anonymous accounts, and apply endpoint rate limiting.
3. Persist a replacement key as pending and preserve the active key until confirmation. Pending material expires after 15 minutes. Store only lookup metadata, PBKDF2 hash/salt, timestamps, and a SHA-256 hash of a cryptographically random 256-bit issue token.
4. On confirmation, validate ownership, token, expiry, and pending material before replacing the active fields. Support an idempotent retry of the latest successful confirmation; new issuance invalidates an older pending confirmation.
5. State clearly that the downloaded JSON contains plaintext recovery credentials and is not encrypted by Diet-Dost. Never log secrets. Do not call ordinary staged key rotation “forward secrecy.”
6. Reconcile all new fields for both fresh and legacy SQLite/Azure SQL schemas and test both command behavior and provider schema paths.

## Consequences
- Users keep their current recovery key until they explicitly confirm replacement.
- The issue token cannot be used as plaintext database state; compromise of the database alone does not reveal the token.
- Pending key material increases the identity schema and requires synchronized SQLite and SQL Server initialization paths.
- Browser-side clearing of strings is best effort and cannot guarantee memory zeroization.
- The plaintext JSON warning is a user-facing security requirement.

## Forward Roadmap Impact & Future Phase Compatibility
The command handlers depend on application repository and unit-of-work abstractions, while the endpoints derive identity at the gateway boundary. The staged state is provider-neutral and reconciled for SQLite and Azure SQL, preserving reuse by future Web and Mobile BFF clients without duplicating recovery logic or exposing user-ID selection to clients.
