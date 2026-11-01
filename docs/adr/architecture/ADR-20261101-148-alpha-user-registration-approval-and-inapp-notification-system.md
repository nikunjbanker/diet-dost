# ADR-20261101-148: Alpha User Registration Approval, In-App Notification System & Real-Time SignalR WebSockets

## Status
Accepted

## Date
2026-11-01

## Context & Problem Statement
Diet-Dost is currently in a private Alpha Preview state. Previously, self-registration automatically activated accounts or required email OTP verification, allowing public users to immediately access the application, invoke AI vision pipelines, and consume token quotas without administrative vetting. Furthermore, the registration UI contained a misleading label `(Mandatory for OTP verification)` next to the email field.

The objectives of this enhancement:
1. Ensure all new registrations remain inactive (`IsActive = false`) until explicitly reviewed and enabled by the application owner (`nikunj_banker@hotmail.com`).
2. Update the registration UI to clarify that email is used for account activation notifications, and inform applicants that their account requires owner approval.
3. Provide an in-app, real-time notification system (avoiding external third-party email provider dependencies) alerting both the applicant (upon approval) and the SuperAdmin (upon new registration).
4. Conduct a rigorous adversarial review of the client-server notification architecture and mitigate potential attack vectors (CSWSH, eavesdropping, XSS, unauthorized elevation).
5. Re-validate the Zero-PII Anonymous registration mode to prevent bypassing the owner approval gate.

## Decision Drivers
- **Alpha Access Governance**: Restrict access to approved alpha testers to protect infrastructure and AI quota limits.
- **Zero External Email Vendor Lock-In**: Provide instant in-app alerts via WebSockets rather than requiring third-party SMTP or cloud communication services.
- **Clean Architecture & Native CQRS**: Encapsulate notifications cleanly across Domain, Application (`IInAppNotificationService`), Infrastructure, and WebGateway layers.
- **Adversarial Resilience**: Mitigate Cross-Site WebSocket Hijacking (CSWSH), unauthorized channel snooping, and stored XSS in notification payloads.
- **Backward Compatibility**: Preserve seeded demo accounts (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`) in a pre-activated state.

## Considered Options
1. **Third-Party Email Notification (SMTP/ACS)**: Requires SMTP credentials, external DNS/SPF/DKIM records, and incurs latency and third-party delivery failure risks.
2. **Short Polling via REST API**: High client overhead, server load, and latency in notifying applicants of approval.
3. **In-App Real-Time SignalR Hub with Dual Persistence**: Native ASP.NET Core SignalR WebSockets mapped to `/hubs/notifications`, with persistent SQLite/Azure SQL audit records in `AppNotifications`.

## Decision Outcome
Chosen Option: Option 3 — In-App Real-Time SignalR Hub with dual persistence.

### Key Architectural & Implementation Enhancements:

1. **Domain & Identity (`Nutrition.Domain`)**:
   - Created `AppNotification` entity implementing `ISoftDeletable` with `RecipientRole`, `RecipientUserId`, `Title`, `Message`, `Type`, `MetadataJson`, `IsRead`, `ReadAtUtc`, and `CreatedAtUtc`.
   - Updated `ApplicationUser.CanLogin` to return a clear, localized rejection reason: *"Your account is pending review and activation by the application owner for this Alpha Preview. Please check back shortly."*

2. **Application Layer & CQRS (`Nutrition.Application`)**:
   - `IInAppNotificationService`: Defined contracts for notifying SuperAdmins of new registrations and alerting applicants upon account approval.
   - `RegisterUserCommand`: Sets `IsActive = false` for newly registered users, generates a cryptographic `RegistrationTicket` (`Guid.NewGuid().ToString("N")`), dispatches `NotifySuperAdminNewRegistrationAsync`, and returns `RequiresOwnerApproval = true` in `RegisterResultDto`.
   - `LoginCommand`: When an unapproved user attempts to sign in, halts authentication with HTTP 403 Forbidden and error code `"AccountPendingApproval"`.
   - `UpdateUserStatusCommandHandler`: Injects `IInAppNotificationService`; when an inactive user is activated (`IsActive = true`), automatically marks `IsEmailVerified = true` and dispatches `NotifyUserAccountActivatedAsync`.

3. **Infrastructure & WebGateway (`Nutrition.Infrastructure` & `Nutrition.WebGateway`)**:
   - Configured `AppNotification` in `DietTrackerDbContext` with indexes on `(RecipientRole, IsRead)` and `(RecipientUserId, IsRead)`. Added idempotent SQLite and Azure SQL migration scripts in `DatabaseInitializationExtensions.cs`.
   - `NotificationHub.cs`: Implemented ASP.NET Core SignalR Hub mapped at `/hubs/notifications`. Automatically assigns authenticated `SuperAdmin` users to the `"SuperAdmins"` group in `OnConnectedAsync`. Exposes `JoinApplicantChannel(string registrationTicket)` with regex validation (`^[a-fA-F0-9]{32}$`).
   - Configured JWT Bearer `OnMessageReceived` handler in `SecurityAndAuthExtensions.cs` to extract `access_token` query parameter for `/hubs/notifications` WebSocket handshakes.
   - `NotificationsController.cs`: REST endpoints for `GET /api/notifications`, `PUT /api/notifications/{id}/read`, and `PUT /api/notifications/read-all`.

4. **Frontend Architecture & UX (`wwwroot`)**:
   - `notification-service.js`: Zero-dependency ES Module implementing native WebSocket connection, SignalR JSON protocol handshakes (`\x1e` record delimiters), ping/pong keepalives, and auto-reconnection with exponential backoff.
   - `auth-gate.html` & `auth-gate.js`:
     - Updated email input label from `(Mandatory for OTP verification)` to `(Used for account notifications & activation)`.
     - Added prominent **Alpha Preview Access Notice** banner informing users that the app owner (`nikunj_banker@hotmail.com`) reviews and activates accounts.
     - Hidden anonymous registration switcher during Alpha to close the bypass path.
     - Created `#form-pending-approval` card showing live applicant status; connects to `NotificationHub` and automatically redirects to Sign In when `AccountApproved` arrives.
   - `header.html` & `main.js`: Added in-app notification bell (🔔) with live unread badge count, dropdown list, mark-all-read action, and click-to-open admin modal for new registrations.
   - `admin-modal.js`: Added `⏳ Pending Review` badge for inactive users and `🔓 Activate & Approve` action button; auto-refreshes user directory upon receiving `NewRegistration` events over SignalR.

5. **Adversarial Security Review & Mitigations**:
   - **Cross-Site WebSocket Hijacking (CSWSH)**: Mitigated by validating the `Origin` header against `Request.Host` in `NotificationHub.OnConnectedAsync`. Disallowed cross-site origins are aborted immediately.
   - **Unauthorized Channel Snooping**: Mitigated by using 128-bit cryptographically secure registration tickets with strict regex enforcement (`^[a-fA-F0-9]{32}$`). Activation payloads contain zero PII or credentials.
   - **Privilege Escalation**: Group assignment for `"SuperAdmins"` is strictly evaluated server-side against authenticated claims in `ClaimsPrincipal`; regular clients cannot join administrative groups.
   - **Stored XSS in Notifications**: All user inputs rendered in notification dropdowns and modals are escaped using safe DOM manipulation and HTML entity encoders.
   - **Anonymous Bypass Closure**: Self-service anonymous registration is disabled during Alpha; only pre-existing seeded recovery keys remain valid for sign-in.

## Consequences
### Positive
- Strict access control prevents unauthorized usage while Diet-Dost is in Alpha preview.
- Real-time in-app feedback loop for applicants without requiring external email delivery infrastructure.
- Streamlined administrative workflow with one-click user activation in the SuperAdmin Governance Console.
- Zero compilation warnings across all 8 projects and 100% pass rate across automated test suites.

### Negative / Trade-Offs
- Requires application owner to manually approve new alpha accounts before users can sign in.
- In-memory SignalR applicant groups require sticky sessions or Redis backplane if scaled beyond a single gateway instance in future multi-node production.

