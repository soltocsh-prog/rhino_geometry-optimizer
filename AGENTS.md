# Engineering Rules

- Prefer the simplest design that satisfies the current requirement. Introduce
  layers, abstractions, and patterns only when they isolate a real dependency,
  policy, or likely change.
- Keep domain logic independent from UI, framework, I/O, and external APIs where
  practical. Place adapters at those boundaries; do not force Clean Architecture
  layers for trivial code.
- Make small, cohesive changes. Preserve existing behavior unless the request
  explicitly changes it; avoid unrelated refactors.
- Add or update automated tests for new domain logic, bug fixes, and regressions.
  Test observable behavior, edge cases, and failures—not implementation details.
- Use test-first development when the desired behavior is clear and the code is
  testable. Otherwise, implement in small increments and add the test immediately
  afterward. Do not fabricate brittle tests merely to satisfy coverage.
- Before declaring work complete, run the most relevant available validation:
  targeted tests, build/type check, formatter/linter, and manual verification when
  integration or UI behavior cannot be automated.
- Diagnose failures from evidence: reproduce when possible, inspect errors/logs,
  form hypotheses, and verify the root cause. Apply the smallest fix that explains
  the observed failure; add a regression test when feasible.
- Treat performance work as evidence-driven: measure or profile first, set a
  concrete target, and measure again after the change. Do not optimize speculatively.
- Handle invalid input, cancellation, resource cleanup, and error paths at
  boundaries. Surface actionable errors; do not silently swallow failures.
- Document non-obvious decisions, constraints, and trade-offs—not self-evident code.
