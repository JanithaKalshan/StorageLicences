# Design Decisions

## 1. Architecture

The solution uses a lightweight Clean Architecture structure with four layers:

- **Presentation/API** — HTTP endpoints, request/response DTOs, and HTTP status mapping.
- **Application** — use-case orchestration, loading required data, and coordinating Domain and Infrastructure.
- **Domain** — entities and scheduling business rules.
- **Infrastructure** — EF Core, SQL Server persistence, migrations, and seed data.

The main reason for this separation is to keep the scheduling rules independent of HTTP and persistence concerns with DDD. Controllers remain thin, while the Domain layer remains testable without EF Core or a database.

I deliberately avoided CQRS/MediatR, domain events, generic repositories, caching, and other additional abstractions because the assignment is small and these would add complexity without providing meaningful value.

## 2. Scheduling Rules and Domain Policy

The seven scheduling rules are implemented in the Domain layer through `PlacementSchedulingPolicy`.

The policy exposes a single scheduling evaluation entry point while keeping the individual rule checks small and readable internally. This keeps the complete scheduling decision in one place without creating seven separate rule-service abstractions.

The Application layer loads the required state — unit, item, licence, existing placements, requester, placement class, scheduled date, and request date — and passes it to the Domain policy.

The Domain does not access `DbContext`, EF Core, HTTP, Infrastructure, or the system clock.

The scheduled date is used when evaluating licence coverage rather than today's date. This is important because a licence may be valid today but not cover a future requested placement date.

## 3. Result and Validation Errors

The existing `Result` / `Result<T>` and `Error` pattern was reused rather than introducing another result abstraction.

The scheduling requirements state that multiple applicable failures should be returned together. Where necessary, the existing Result implementation was extended minimally to support multiple errors while preserving its existing behavior.

Scheduling failures use stable rule codes so that the API can return structured errors and the Vue frontend can display all applicable failures without implementing the business rules itself.

For example:

- `LICENCE_NOT_COVERING_DATE`
- `PALLET_CAPACITY_EXCEEDED`
- `PALLET_GAP_TOO_SMALL`
- `ITEM_INTAKE_DATE_MISSING`
- `SCHEDULING_DATE_TOO_FAR`

The frontend treats these as API validation results rather than attempting to reproduce the Domain logic.

## 4. Asserted Requester Identity

The assignment does not require authentication or user management.

For scheduling, the API therefore accepts an asserted requester identifier. The identifier is compared with the current licence holder in the Domain layer.

`HolderId` is treated as an opaque identifier rather than introducing User, Company, Role, or identity entities.

This keeps the implementation aligned with the assignment while leaving a clear point where real authentication/identity information could be introduced in a production system.

## 5. Availability and `asOf`

The availability endpoint accepts only a unit ID and an `asOf` date.

It therefore cannot meaningfully evaluate rules that require information that is not part of the request, such as:

- requester identity
- item intake date
- request date

The availability implementation evaluates only rules that can be determined from the available unit, licence, placement, and `asOf` information.

The individual scheduling-rule checks are reused where appropriate instead of running the complete placement scheduling policy with fabricated values.

This avoids coupling the availability use case to inputs that it does not have and prevents the frontend/API from making assumptions about requester or item information.

## 6. Licence Transfer — Section 5

The assignment deliberately leaves the behavior of already scheduled placements when a licence transfers unspecified, and transfer functionality is not implemented.

### Chosen assumption

A unit can have only one licence in force at a time, and the unit's capacity belongs to the unit rather than being partitioned between licence holders.

Existing scheduled placements therefore remain associated with the unit and continue to count according to the normal placement rules. A future licence holder does not receive a separate capacity allocation.

### Alternative

An alternative would be to associate placements/capacity explicitly with a licence and partition the unit's capacity between different licence holders.

This would require additional domain concepts and changes to scheduling, availability, API contracts, and UI behavior.

### What would change the decision

I would revisit this decision if the business clarified that multiple licence holders can have simultaneous rights to the same unit or that capacity must be reserved/partitioned per licence.

The current design deliberately keeps the unspecified transfer behavior isolated so that it can be changed later without introducing transfer functionality prematurely.

## 7. EF Core and Query Design

EF Core is used for persistence with SQL Server.

The unit-list endpoint performs filtering, counting, paging, and projection at the database level rather than loading the complete dataset into memory.

This avoids:

- client/application-side paging
- unnecessary entity loading
- N+1 queries

The API returns only the page requested by the caller while `totalCount` represents the filtered result count before paging.

Indexes were considered based on the actual query patterns, particularly unit/placement/licence relationships used by the required endpoints. Indexes are not used to encode Domain business rules.

The query and index choices should be verified against the seeded dataset and, where possible, SQL execution-plan/timing evidence.

## 8. Frontend Boundary

The Vue frontend is intentionally kept simple.

The frontend is responsible for:

- displaying units
- filtering and paging through the API
- displaying unit details
- schedule placements
- displaying API results and validation errors

The frontend does not duplicate the seven scheduling rules.

This keeps the backend as the single source of truth for scheduling behavior and prevents the Domain logic from being implemented a second time in JavaScript/TypeScript.

## 9. Testing Approach

The test suite is split between Domain and API-level behavior.

Domain tests focus on the seven scheduling rules and important interactions, including combined failures, exactly 12 months of pallet spacing, future pallet conflicts, cancelled placements, and the 24-month boundary.

API tests focus on endpoint behavior, request/response contracts, validation/error mapping, and persistence behavior.

This separation avoids duplicating the Domain business-rule implementation in API tests while still verifying that the API correctly exposes and orchestrates the Domain behavior.

### Integration testing

I deliberately did not add full HTTP integration tests using `WebApplicationFactory` or an equivalent full API pipeline.

The main reason is that, for this assignment, the additional setup and maintenance complexity would be relatively high compared with the value provided. Full integration tests would require coordinating the ASP.NET Core application pipeline, SQL Server test database lifecycle, migrations/seeding, HTTP requests, and test isolation.

Instead, I focused the automated test coverage on the business rules and application behaviour, where failures are more directly attributable to a specific requirement.

This is a deliberate trade-off rather than an assumption that integration testing is unnecessary in a production system.

### What is not covered

The following areas are not covered by automated integration tests:

- Full HTTP request-to-database pipeline testing.
- Controller routing and HTTP serialization/deserialization as an end-to-end flow.
- SQL Server migration execution as part of the automated test suite.
- End-to-end browser testing of the Vue application.
- Concurrent placement requests/race-condition scenarios.

These are recognised gaps. In a production system, I would add integration tests around the highest-risk API/database boundaries and, where appropriate, a small number of end-to-end tests covering the most important user journeys.

For this take-home, I prioritised a smaller, maintainable test suite with strong coverage of the domain rules rather than adding a larger integration-test infrastructure that would significantly increase the scope of the solution.

## 10. Performance and Seed Data

The database is seeded with at least 5,000 units and realistic variations so that query behavior can be evaluated against a representative dataset.

The unit-list query uses server-side filtering, paging, and projection.

Performance verification is based on the seeded dataset rather than assuming that a query is efficient from code inspection alone.

### Query and Indexing

GET /api/units uses server-side filtering, projection, and pagination. The query was evaluated against the seeded dataset containing 5,000 units.

The primary Unit key is a clustered index on Id. Placements use a composite index on (UnitId, PlacementClass, Status, ScheduledDate) to support the placement filtering and scheduling queries. Storage licences use an index on (UnitId, GrantDate) to support licence lookups.

The actual SQL Server execution plan was reviewed. The plan uses the existing Unit clustered index and non-clustered indexes for Placement and StorageLicence access. The licence lookup includes a key lookup for additional licence columns, but this was not optimized further because measured execution time was already low and the dataset is relatively small.

With 5,000 seeded units and a page size of 100, the measured SQL execution time was approximately 2ms, with 0ms reported CPU time. The query produced 2 logical reads for Units, 10 for Placements, and 198 for StorageLicences.

The generated query contains separate placement counts for pallet and box occupancy. Although these could be consolidated into a single aggregation, the measured placement workload was only 10 logical reads and therefore the simpler query was retained rather than introducing additional query complexity.

Statics OutPut:

(11 rows affected)

SQL Server Execution Times:
CPU time = 0 ms, elapsed time = 0 ms.

SQL Server Execution Times:
CPU time = 0 ms, elapsed time = 0 ms.

SQL Server Execution Times:
CPU time = 0 ms, elapsed time = 0 ms.

SQL Server Execution Times:
CPU time = 0 ms, elapsed time = 0 ms.

SQL Server Execution Times:
CPU time = 0 ms, elapsed time = 0 ms.

(100 rows affected)
Table 'StorageLicences'. Scan count 1, logical reads 198, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.
Table 'Units'. Scan count 1, logical reads 2, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.
TableSo I nee 'Placements'. Scan count 4, logical reads 10, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.

SQL Server Execution Times:
CPU time = 16 ms, elapsed time = 2 ms.

Completion time: 2026-09-10T11:04:28.1765209+05:30

API-level response times were also measured after application/database warm-up and remained below the assignment's 300ms target.
![GET api/Units/ response time](<Screenshot 2026-09-10 100148.png>)

## Known Limitations and Deliberate Gaps

### Duplicate item scheduling

The current model does not prevent the same item from being scheduled more than once for the same or different placement dates.

I identified this as a potential domain/data-integrity concern, but the assignment requirements do not specify a rule preventing an item from having multiple placements. Therefore, I deliberately did not introduce an additional business rule, database constraint, or uniqueness requirement that is not part of the stated scope.

If this were a production requirement, I would first clarify the expected item lifecycle with the business. If an item is intended to occupy only one placement at a time, I would then add an explicit domain rule and corresponding tests, rather than enforcing an assumption at the database level.

This is therefore a recognised limitation of the current implementation rather than an overlooked requirement.
