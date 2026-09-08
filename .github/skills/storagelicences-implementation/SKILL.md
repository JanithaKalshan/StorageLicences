---
name: storagelicences-implementation
description: Implementation details for the OpusXenta Senior Software Engineer (.NET/Vue/Fullstack) take-home assignment. Includes domain model, scheduling rules, and architectural guidelines.
---

# OpusXenta Take-Home — Implementation Skill

## Purpose

Use this file as project knowledge while implementing the OpusXenta Senior Software Engineer (.NET/Vue/Fullstack) take-home assignment.

Follow the assignment requirements exactly. Prefer simple, maintainable solutions over extra features or abstractions.

## Stack

* Backend: .NET 10 Web API, C#
* Frontend: Vue 3
* Database: SQL Server
* ORM: EF Core
* Tests: automated unit/integration tests as appropriate
* Dates: `DateOnly`; no time/timezone logic

## Scope

Build only:

* Units
* Storage licences
* Items
* Placements
* Placement scheduling/validation
* Unit list/detail
* Unit availability

Do NOT add:

* Authentication/user management
* Transfer endpoint/UI
* Billing
* Checkout/removal
* Audit trails
* Soft deletes
* Reporting/admin dashboards
* Message brokers
* Microservices
* Event sourcing
* Caching layers
* Unnecessary Docker/orchestration
* Generic repositories or unnecessary framework abstractions

## Architecture

Use lightweight Clean Architecture:

* Presentation/API
* Application
* Domain
* Infrastructure

Business rules belong in the Domain layer, not controllers or Vue.

Recommended domain entities:

* `Unit`
* `StorageLicence`
* `Item`
* `Placement`

Recommended enums:

* `PlacementClass`: `Pallet`, `Box`
* `PlacementStatus`: `Scheduled`, `Completed`, `Cancelled`

Keep the domain independent of:

* EF Core
* `DbContext`
* SQL
* HTTP
* Infrastructure
* system clock

Pass `DateOnly` values explicitly into domain operations.

## Domain Model

### Unit

Contains:

* Id
* PalletCapacity: 0–3
* BoxCapacity: 0–8
* relationship to StorageLicence history
* relationship to Placement history

Licence and placement history are stored in their respective tables using UnitId foreign keys. Do not serialize these histories into Unit or duplicate them as JSON.

A unit must never have more than one licence in force at a time.

### StorageLicence

Contains:

* Id
* UnitId
* HolderId
* GrantDate
* TermYears
* optional SurrenderedDate

Licence coverage:

`GrantDate + TermYears` is exclusive.

Example:

* Grant: `2016-03-10`
* Term: 10 years
* Covers through `2026-03-09`
* `2026-03-10` is outside the licence

A licence can transfer, lapse, or be surrendered.

### Item

Contains:

* Id
* Reference
* Description
* optional IntakeDate

Only IntakeDate affects scheduling rules.

### Placement

Contains:

* Id
* UnitId
* ItemId
* PlacementClass
* ScheduledDate
* Status

Cancelled placements count toward neither capacity nor pallet-spacing rules.

## Scheduling Rules

Evaluate all seven rules and return all applicable failures, not only the first failure.

### Rule 1 — Unit exists

The requested unit must exist.

This may be handled by the application layer as a not-found result before invoking the domain policy.

### Rule 2 — Licence covers scheduled date

A valid licence must:

* belong to the unit
* not be surrendered before/on the relevant date
* cover the requested scheduled date

Check the **scheduled date**, not today's date.

### Rule 3 — Current licence holder

The requester must match the current licence holder.

No authentication system is required.

The scheduling request should carry an asserted requester identifier, for example:

```json
{
  "assertedRequesterId": "COMPANY-ABC"
}
```

`StorageLicence.HolderId` is an opaque identifier.

Do NOT create User/Company/Role/Identity entities unless the existing solution already requires them.

### Rule 4 — Capacity

Count only `Scheduled` and `Completed` placements.

Do not count `Cancelled`.

Compare the count with the relevant unit capacity:

* Pallet → PalletCapacity
* Box → BoxCapacity

### Rule 5 — Pallet spacing

Only pallet placements are subject to the spacing rule.

Compare the requested pallet date against **all scheduled and completed pallet placements in the same unit**, both before and after the requested date.

There must be at least 12 calendar months between placements.

Exactly 12 months is valid.

Cancelled placements are ignored.

Boxes are ignored.

Example:

* Existing pallet: `2025-11-01`
* Requested pallet: `2026-05-15`
* Less than 12 months → reject

Also check future existing pallet placements.

### Rule 6 — Item intake date

The item must have a recorded IntakeDate.

The scheduled date must not be earlier than IntakeDate.

Therefore:

* Missing IntakeDate → reject
* ScheduledDate < IntakeDate → reject
* ScheduledDate == IntakeDate → valid

### Rule 7 — Scheduling window

The scheduled date must be no more than 24 calendar months after the request date.

Use:

`ScheduledDate <= RequestDate.AddMonths(24)`

Exactly 24 months is valid.

Do not read the current system date inside the domain.

## Scheduling Policy

Prefer one domain policy such as:

`PlacementSchedulingPolicy`

Expose one public evaluation/validation operation.

Internally keep the seven checks small and readable.

Do NOT create seven separate rule-service interfaces/classes.

The application layer should load the required state and pass it to the domain policy:

* Unit
* Item
* current licence
* relevant placements
* asserted requester
* placement class
* scheduled date
* request date

## Result Pattern

Use the existing `Result` / `Result<TValue>` pattern in `DomainLayer/Common/Result.cs`.

Current API:

```csharp
public static Result Success();
public static Result Failure(Error error);

public static Result<TValue> Success<TValue>(TValue value);
public static Result<TValue> Failure<TValue>(Error error);
```

The existing implementation accepts a single `Error`.

Do NOT invent a competing result abstraction.

The scheduling assignment requires combined failures to be reported together. Inspect existing Result/Error usage before changing it.

If multiple errors cannot be represented cleanly, make the smallest possible extension to the existing `Result` implementation, preserving existing behavior.

A possible extension is:

```csharp
public static Result Failure(IEnumerable<Error> errors);
```

Do not assume this overload already exists.

Use structured rule errors with stable codes/messages.

Suggested codes:

* `LICENCE_NOT_COVERING_DATE`
* `LICENCE_SURRENDERED`
* `NOT_CURRENT_HOLDER`
* `PALLET_CAPACITY_EXCEEDED`
* `BOX_CAPACITY_EXCEEDED`
* `PALLET_GAP_TOO_SMALL`
* `ITEM_INTAKE_DATE_MISSING`
* `ITEM_NOT_AVAILABLE_ON_DATE`
* `SCHEDULING_DATE_TOO_FAR`

Use the project's existing `Error` implementation/conventions where possible.

## Required APIs

### GET `/api/units`

List units with:

* current occupancy
* remaining capacity by class
* filtering
* server-side paging

Required response shape:

```json
{
  "items": [],
  "totalCount": 5000,
  "page": 1,
  "pageSize": 50
}
```

Rules:

* `items` contains only the requested page.
* `totalCount` is the count after filtering but before paging.
* `page` is 1-based.
* `pageSize` is the requested page size.
* Filtering and paging MUST execute in SQL.
* Never load all units and filter/page in memory.
* Project directly to DTOs.
* Avoid N+1 queries.

### GET `/api/units/{id}`

Return unit detail including:

* licence information/history as appropriate
* all placements

### GET `/api/units/{id}/availability?asOf={date}`

Return whether the unit can accept:

* pallet
* box

and explain why not.

This endpoint does not have requester/item/request-date context.

Therefore document which rules can meaningfully be evaluated here.

Meaningful checks include:

* unit existence
* licence validity for `asOf`
* capacity
* pallet spacing

Requester, item intake date, and 24-month request window require information unavailable to this endpoint and should not be fabricated.

### POST `/api/placements`

Schedule a placement and apply all scheduling rules.

Suggested request:

```json
{
  "unitId": 123,
  "itemId": 456,
  "placementClass": "Pallet",
  "scheduledDate": "2026-05-15",
  "assertedRequesterId": "COMPANY-ABC"
}
```

## Section 5 — Licence Transfer

Do NOT implement transfer flow/API/UI.

The assignment deliberately leaves the behavior of already scheduled placements when a licence transfers unspecified.

Document:

1. Your chosen assumption.
2. At least one reasonable alternative.
3. What evidence/change in requirements would cause you to change the decision.
4. Enough detail that the implementation could be changed later.

This decision must be captured in `DECISIONS.md`.

## EF Core

Use separate entity configurations where appropriate:

* `UnitConfiguration`
* `StorageLicenceConfiguration`
* `ItemConfiguration`
* `PlacementConfiguration`

Configure:

* keys
* relationships
* required/optional fields
* string lengths
* structural constraints
* useful indexes

Do NOT encode the seven business rules as database constraints.

## Query Performance

`GET /api/units` must remain under the assignment's 300ms target with the seeded dataset.

At least 5,000 units must exist.

Use SQL projection and server-side filtering/paging.

Avoid:

* `ToList()` before filtering
* `ToList()` before paging
* N+1 queries
* loading unnecessary navigation properties

Add indexes based on actual query patterns.

Document indexing decisions.

Verify performance with realistic seeded data and, where possible, SQL execution plan/timing evidence.

Do not add indexes blindly.

## Seed Data

Seed at least 5,000 units with realistic variation.

Include:

* capacities from the allowed ranges
* empty/partial/full units
* zero-capacity classes
* active licences
* licences close to lapse
* lapsed licences
* surrendered/historical licences
* scheduled placements
* completed placements
* cancelled placements
* recent pallet placements
* items with and without IntakeDate
* data capable of exercising scheduling-rule scenarios

Use deterministic generation.

Do NOT hard-code thousands of records into migrations.

Do NOT call `SaveChanges()` once per entity.

Prefer:

```csharp
context.Units.AddRange(units);
context.Items.AddRange(items);
context.StorageLicences.AddRange(licences);
context.Placements.AddRange(placements);
await context.SaveChangesAsync();
```

Use `AddRange` and appropriate batching to avoid thousands of database round trips.

Keep seed logic maintainable and reproducible.

## Tests

Write a small, high-value test suite focused on business rules and interactions.

Cover at least:

* valid pallet
* valid box
* invalid/non-covering licence
* surrendered licence
* wrong requester
* pallet capacity exceeded
* box capacity exceeded
* cancelled placement ignored for capacity
* pallet gap less than 12 months
* exactly 12 months succeeds
* future pallet placement causing spacing failure
* box ignores pallet spacing
* missing intake date
* scheduled date before intake date
* scheduled date equal to intake date
* more than 24 months after request date
* exactly 24 months succeeds
* combined failures returned together

Document deliberate test gaps.

## Documentation

Provide:

### README.md

Include:

* project overview
* prerequisites
* setup
* database configuration
* migrations
* seed instructions
* how to run API
* how to run Vue app
* how to run tests
* API overview
* performance verification

### DECISIONS.md

Approximately 1–2 pages.

Explain important architectural/design decisions, especially:

* architecture boundaries
* domain policy
* Result handling
* asserted requester identity
* availability `asOf` semantics
* Section 5 transfer assumption
* EF/query/index choices
* deliberate simplifications

### AI-NOTES.md

Record 3–5 concrete AI interventions.

For each intervention explain:

* what AI helped with
* what was accepted/changed/rejected
* how it was verified

Keep notes credible and specific.

## Git

Use a private GitHub repository.

Use feature branches and pull requests.

Have at least 3 coherent PRs.

Use meaningful commits.

Do NOT create one giant commit.

Do NOT squash the entire implementation into one commit.

Possible progression:

1. Foundation/domain/database
2. Scheduling/API/tests
3. Vue/frontend/performance/docs

The exact grouping can vary if the history remains coherent.

## Implementation Order

Prefer this order:

1. Inspect existing solution and conventions.
2. Establish domain entities/value types/enums.
3. Implement licence/date semantics.
4. Implement scheduling policy and Result/error handling.
5. Add EF Core mappings/migrations.
6. Add deterministic seed data.
7. Implement API endpoints.
8. Add tests.
9. Implement Vue UI.
10. Verify query performance/indexes.
11. Complete README, DECISIONS.md and AI-NOTES.md.
12. Review Git history and clean up unnecessary complexity.

## Final Verification

Before considering the assignment complete, verify:

* All seven scheduling rules are implemented.
* Rule 2 uses the scheduled date, not today.
* Rule 5 checks both past and future pallet placements.
* Exactly 12 months is valid.
* Exactly 24 months is valid.
* Cancelled placements are ignored where required.
* Boxes do not use pallet-spacing rules.
* Combined failures are returned together.
* Domain rules are not duplicated in controllers/Vue.
* `GET /api/units` filters/pages in SQL.
* Pagination response matches the documented JSON contract.
* No N+1 queries exist.
* Seed uses `AddRange`/batching rather than per-record `SaveChanges`.
* At least 5,000 units are seeded.
* Section 5 is explicitly documented.
* Tests cover rule interactions and deliberate gaps.
* `DECISIONS.md` and `AI-NOTES.md` are complete.
* Git contains coherent feature branches/PRs and meaningful commits.
* No unnecessary scope has been added.

## Guiding Principle

Build the smallest complete solution that demonstrates strong domain reasoning, clean boundaries, correct SQL/query behavior, useful tests, and clear engineering decisions.

More code or more features are not better.
