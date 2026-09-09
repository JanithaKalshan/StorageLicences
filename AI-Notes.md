# AI Notes

Implementation details and project requirements are provided to Copilot through the project skill file:

`.github/skills/storagelicences-implementation`

## AI Intervention 1 — Domain and Database Foundation

**Prompt:**

> Create the Domain entities, EF Core configurations, and database seed data implementation based on the attached skill file. All primary keys must be integers. Implement all 7 domain scheduling rules in the Domain layer, following the architecture and Result pattern defined in the skill file.

## AI Intervention 2 — Domain Unit test

**Prompt:**

> The test project has already been created, and a Domain folder already exists inside the test project. Use this existing Domain folder to add all Domain-specific unit tests.Use xUnit and follow the existing Clean Architecture structure and project conventions. Use the attached skill file as the source of truth for the Domain scheduling rules and expected behavior.Test PlacementSchedulingPolicy and related Domain behavior directly. Keep these as true unit tests without EF Core, SQL Server, HTTP, or API dependencies. Use the existing Result and Error pattern. Cover all seven scheduling rules, including boundary and interaction cases: valid pallet and box placements, invalid/non-covering and surrendered licences, wrong requester, pallet and box capacity, cancelled placements being ignored for capacity, pallet spacing less than 12 months, exactly 12 months being allowed, future pallet spacing violations, boxes ignoring pallet spacing, missing/invalid/equal intake dates, more than/exactly 24 months scheduling window, and combined failures returning all applicable errors. Use clear test names that describe the business scenario and expected outcome. Reuse the existing Domain entities, PlacementSchedulingRequest, PlacementSchedulingPolicy, Result, Error, and other helpers instead of creating duplicate Domain models or business logic in the test project. Do not modify the production Domain implementation just to make tests pass unless a genuine defect is identified. After implementation, build the solution and run all tests. Fix only test or implementation issues that are necessary and report the tests added and any deliberate test gaps

## AI Intervention 3 — API Controllers and Infrastructure

**Prompt:**

> Implement the API Controllers and Infrastructure/persistence layer required by the assignment and the storagelicences-implementation skill. First inspect the existing solution and follow its current structure and conventions where they do not conflict with the skill or assignment.
> Implement the four required API endpoints:
> GET /api/units
> GET /api/units/{id}
> GET /api/units/{id}/availability
> POST /api/placements
> Keep controllers thin. Put orchestration in Application and EF Core/database access in Infrastructure.Use the existing DbContext and Result/Error patterns. Do not introduce a generic repository or unnecessary abstractions. Only add a focused repository/persistence abstraction if the existing structure genuinely benefits from it.For POST /api/placements, validate through the existing Domain scheduling policy and persist only after successful validation. The persistence operation must be transactional.For GET /api/units, ensure filtering, paging, and projection are executed server-side and avoid N+1 queries.Need to consider SQL performance For GET /api/units/{id}/availability, reuse the existing individual scheduling-rule methods where appropriate rather than running the complete placement validation. Only evaluate rules that can be meaningfully determined from the unit and asOf date.Follow all API contracts, business rules, error handling, performance requirements, and scope boundaries defined in the skill file.Do not duplicate business rules in the API or Infrastructure layers.After implementation, build the solution, run relevant tests, verify the endpoints, and report any remaining gaps or issues.

# AI Intervention 4 — API Controllers Test

**Prompt:**

> Implement the API-layer tests required by the assignment and `storagelicences-implementation` skill.
> First inspect the existing test project and API/Application implementation. Follow the existing testing conventions and reuse existing test helpers, `Result`/`Error` types, DTOs, and fixtures where possible.
> Add high-value tests for the four required API endpoints:
> `GET /api/units`
> `GET /api/units/{id}`
> `GET /api/units/{id}/availability`
> `POST /api/placements`
> Focus on API behavior, request/response contracts, validation/error mapping, and Application/Infrastructure interaction. Do not duplicate the Domain unit tests.
> For `GET /api/units`, test server-side filtering, paging, response metadata, and relevant edge cases.
> For `GET /api/units/{id}`, test successful detail retrieval and not-found behavior.
> For `GET /api/units/{id}/availability`, test the applicable availability rules and that only meaningful rules are evaluated for the available inputs.
> For `POST /api/placements`, test successful scheduling, validation failures, combined failures, and that a failed validation does not persist a placement.
> Use the appropriate test type for the existing architecture (unit/integration/API tests) rather than introducing unnecessary testing infrastructure. Keep the tests deterministic and independent of the system clock.
> After implementation:
>
> 1. Build the solution.
> 2. Run all relevant tests.
> 3. Fix genuine test or implementation issues.
> 4. Report the tests added and any deliberate test gaps.
