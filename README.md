# Timetable Solver — Full-School CP-SAT Timetable Engine

ASP.NET Core 8 Web API that generates weekly timetables for a 41-section school using
**Google OR-Tools CP-SAT**, built for the RDPL .NET developer technical assessment
(`Candidate_Timetable_Assessment.md`).

The solution loads the **real dataset provided in this package** (`sections.json`,
`bell-schedule.json`, `CLASS_WISE_SUBJECTS.md`, `TEACHER_CLASS_ASSIGNMENTS.md`,
`school-sample.json`) — nothing is invented or hard-coded.

Verified in this environment: `dotnet build` (0 warnings/errors), `dotnet test`
(7/7 passing, test project wired into the `.sln`), and both data-source modes
exercised live end-to-end against the running API.

---

## 1. Solution layout

```
TimetableSolver.sln
src/
  TimetableSolver.Domain          Entities & enums. No dependencies on anything.
  TimetableSolver.Application     Interfaces (repositories/services), DTOs, options, exceptions.
  TimetableSolver.Infrastructure  File-based repositories (JSON + Markdown parsers), data-joining services.
  TimetableSolver.Solver          Google.OrTools CP-SAT model + solver, wrapped behind ITimetableGenerationService.
  TimetableSolver.Api             ASP.NET Core Web API — Controllers, Program.cs, appsettings, /Data (dataset files).
tests/
  TimetableSolver.Tests           xUnit: parser tests + one CP-SAT end-to-end smoke test.
```

This follows **Controller → Service → Repository**, with dependencies pointing inward
(`Api → Solver/Infrastructure → Application → Domain`), so `Domain` and `Application` have
zero knowledge of ASP.NET Core, OR-Tools, JSON, or Markdown:

| Layer | Depends on | Knows about |
|---|---|---|
| Domain | nothing | Entities/enums only |
| Application | Domain | Interfaces, DTOs, options — no implementation detail |
| Infrastructure | Application, Domain | File I/O, JSON/Markdown parsing |
| Solver | Application, Domain | Google.OrTools.Sat |
| Api | all of the above | HTTP, DI composition |

This means, for example, the entire CP-SAT engine could be replaced with a different solver
library, or the data source switched from files to a database, without touching `Domain`,
`Application`, or the controllers.

```
TimetableController          (Api)
  -> ISchoolDataService       (Application interface)
       -> FullDatasetSchoolDataService / SampleSchoolDataService   (Infrastructure)
            -> ISectionRepository, ICurriculumRepository, ITeacherAssignmentRepository,
               IBellScheduleRepository, ISampleSchoolRepository   (Application interfaces)
                 -> Json*Repository / Markdown*Repository          (Infrastructure)
  -> ITimetableGenerationService (Application interface)
       -> OrToolsTimetableGenerationService                       (Solver)
            -> CpSatModelBuilder -> IConstraintRule[]              (Solver)
            -> CpSatSolverEngine                                   (Solver)
```

The controller has **no business logic** — every action is "call a service, map the result,
return it." No file I/O, no LINQ joins, no CP-SAT calls in `TimetableController`.

---

## 2. Running it

### Visual Studio 2022
1. Open `TimetableSolver.sln`.
2. Set `TimetableSolver.Api` as the startup project (already the default).
3. F5. Swagger opens at `/swagger`.

### CLI
```bash
dotnet restore
dotnet build
dotnet run --project src/TimetableSolver.Api
dotnet test
```

`DataSource:Mode` in `appsettings.json` defaults to `FullDataset`; the `Development`
environment (the default launch profile) overrides it to `Sample` for a fast local smoke
test. Pass `--no-launch-profile` (or set `ASPNETCORE_ENVIRONMENT=Production`) to exercise
the full 41-section dataset from the command line.

---

## 3. API surface

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/timetable/load-data` | Loads + normalizes the dataset, returns summary + data conflicts |
| `POST` | `/api/timetable/generate` | Builds the CP-SAT model and solves it |
| `GET` | `/api/timetable/sections` | All section timetables from the last generation |
| `GET` | `/api/timetable/sections/{id}` | One section's timetable |
| `GET` | `/api/timetable/teachers` | All teacher timetables from the last generation |
| `GET` | `/api/timetable/conflicts` | Data conflicts + scheduling conflicts |

Call `load-data` then `generate` (in that order) — `generate` will auto-load if you skip the
first call.

---

## 4. Two data-source modes

Controlled by `DataSource:Mode` in `appsettings.json` (`FullDataset` by default;
`appsettings.Development.json` overrides it to `Sample` for a fast local smoke test):

- **`FullDataset`** — the real 41-section problem: `sections.json` + `bell-schedule.json` +
  `CLASS_WISE_SUBJECTS.md` + `TEACHER_CLASS_ASSIGNMENTS.md`, joined by
  `FullDatasetSchoolDataService`. Loads in well under a second (168 schedulable curriculum
  items across 41 sections); solved with a 120s time budget.
- **`Sample`** — the self-contained `school-sample.json` (5 sections, teachers and assignments
  already resolved). Solves to `Optimal` in ~1-1.3s; used by the automated end-to-end test.

Both modes produce the exact same `SchoolModel` shape, so the CP-SAT layer never needs to know
which one is active.

---

## 5. Data assumption you should know about

The assessment brief names `TEACHER_CLASS_ALLOCATION.xlsx` (587 rows) as the **primary,
section-level** assignment source. That file was not included in this package — only
`TEACHER_CLASS_ASSIGNMENTS.md`, which gives **class-level totals** (one row per class, summed
across every section of that class; e.g. "Class 1 Art Education: 20 periods/week" is the total
across Grade 1's 4 sections, not one section's requirement).

`FullDatasetSchoolDataService` resolves this by:
1. Trusting **`CLASS_WISE_SUBJECTS.md`** for the authoritative *per-section* `periodsPerWeek` /
   `periodsPerDay` — that file is explicit that its numbers are already normalized to 52 ppw for
   an individual section.
2. Trusting **`TEACHER_CLASS_ASSIGNMENTS.md`** only to resolve *which teacher* delivers a given
   class-subject pair, applying that same teacher to every section of the class.
3. Reporting every class-subject that resolves to `UNASSIGNED-TT`, a zero class-level workload,
   or no assignment row at all as a `DataConflict` — **once per affected section**, so you can
   see exactly which sections are blocked, not just which classes.

**This is the direct cause of `POST /generate` returning `INFEASIBLE` on the full 41-section
run** — see §11 below. It is not a solver bug; it is a real consequence of not having
section-level data. Swapping in the real xlsx loader (`ClosedXML`, per the assessment's
suggested package) would resolve it; the `ITeacherAssignmentRepository` interface is already
shaped to support a section-level source with zero changes to anything above it.

---

## 6. Known data gaps (reported, never silently dropped)

| Gap | Where reported |
|---|---|
| 92 `UNASSIGNED-TT` placeholder rows | `DataConflictType.UnassignedTeacher`, one per affected section-subject |
| Zero-workload rows | `DataConflictType.ZeroWorkload` |
| Class-subjects with no assignment row at all | `DataConflictType.MissingAssignment` |
| Ambiguous class-subject (2+ teachers listed) | `DataConflictType.AmbiguousAssignment` — highest-workload row is used, rest are reported |
| Pre-Primary / KG sections (no curriculum in `CLASS_WISE_SUBJECTS.md`) | `DataConflictType.MissingCurriculum`; those sections are excluded from generation (`SchedulableSections`) — matches the brief's optional P4 rule |
| Teacher whose total assigned load exceeds weekly capacity | `DataConflictType.TeacherOverload`, computed post-solve when the model is `INFEASIBLE` |
| Empty `teacher_availability` | Assumed fully available (documented in `TIMETABLE_GENERATION_DATA_REQUIREMENTS.md` itself; no H2 rule needed since there's nothing to restrict against) |
| No Computer Lab / Science Lab rooms | Room rules (R1/R2) not implemented — rooms are empty in every provided file, so there is nothing to model; documented as out-of-scope per the brief |

All of these show up in the `dataConflicts` array of `POST /api/timetable/load-data` and
`POST /api/timetable/generate`.

---

## 7. Rule-ID → implementation map

| Rule ID | Rule | Class |
|---|---|---|
| H1 | No teacher double-booking | `Rules/NoTeacherDoubleBookingRule` |
| H3 | One lesson per section per slot | `Rules/OneLessonPerSectionSlotRule` |
| H4 | No lessons in breaks | Structural — break periods never get a variable (`JsonBellScheduleRepository` only expands `Teaching`-type periods) |
| H5, B1, L2 | Block subjects (`periodsPerDay >= 2`) never split across a non-adjacent pair, never straddle a break | `Rules/BlockConsecutivePairingRule` |
| H7 | Daily per-subject max | `Rules/DailySubjectMaxRule` |
| H8 | Weekly curriculum totals | `Rules/WeeklyCurriculumTotalsRule` |
| H10, DATA-1/T4 | Zero-workload / `UNASSIGNED-TT` excluded | Structural — `CurriculumItem.IsSchedulable`; never gets a variable |
| T2 | Only the formally assigned teacher is ever used | Structural — a variable only exists for the resolved `TeacherId` |
| G1 | Games/Library never in first period pair | `SlotEligibilityPolicy.DefaultSlotEligibilityPolicy` (applied at variable-creation time) |
| G2 | Class 11/12: Games & Library not same day | `Rules/Class11And12GamesLibrarySameDayBanRule` |
| L1 | Same-first-word subjects not same day | `Rules/SameFirstWordSameDayBanRule` — hard, with a documented minimum-necessary relaxation (see §8) |
| L3 | Max 3 consecutive same subject | Not separately coded — every `periodsPerDay` value in `CLASS_WISE_SUBJECTS.md` is 1 or 2, so H7 already caps same-subject runs at 2 per day; L3's limit of 3 can never be reached with this dataset. Documented rather than adding a dead constraint. |
| PR-MATH | Prefer Mathematics in the morning | `MathMorningPreferenceObjective` (soft, minimized) |
| OPT-WORKLOAD | Teacher max periods/day/week | `Rules/TeacherWorkloadCapRule` (no-op unless the data source supplies the cap — `school-sample.json` does; `TEACHER_CLASS_ASSIGNMENTS.md` does not carry per-teacher caps, so it's inert in `FullDataset` mode) |
| R1/R2, M1–M6 | Room mapping, merged sections | Not implemented — no room or merged-section data in any provided file; documented as skipped rather than silently ignored |
| SE*, SA*, FF* | Substitution engine, force-fill | Out of scope per the brief |

Every rule is a small, single-purpose class implementing `IConstraintRule`
(`Apply(CpModel, SchoolModel, TimetableVariables) -> IReadOnlyList<string>` — the return value
is advisory notes for reporting, e.g. rule relaxations, empty for rules that never relax),
registered explicitly (not via reflection) in
`TimetableSolver.Solver.DependencyInjection.ServiceCollectionExtensions` — that list is the
one place that decides what's actually enforced, which keeps this table honest.

`Data/scheduling-rules.json` is the assessment's own machine-readable rule spec — this table is
the mapping from that spec to actual code. The file itself is **reference documentation only**;
nothing in the solver parses it at runtime (the `DataSource:SchedulingRulesFile` option that
used to point at it was dead configuration and has been removed — every rule above is a
directly-coded `IConstraintRule`, which is also what the assessment brief's own `cpSatHint`
column recommends).

---

## 8. Modeling notes

- **Variables**: one `BoolVar` per (section, schedulable curriculum item, day, period) —
  created only when `ISlotEligibilityPolicy` allows it (this is where G1 lives) and only for
  items that passed data resolution (`IsSchedulable`).
- **Block pairing (H5/B1/L2)**: `BellSchedule` pre-computes a `PairGroup` for every slot
  (break-free adjacent pairs — see `TeachingSlot.PairGroup`). `BlockConsecutivePairingRule`
  forbids any two same-day periods of a `periodsPerDay >= 2` item **unless** they are the two
  adjacent slots of one pair-group. This allows a legitimate lone single on a day (needed
  whenever `periodsPerWeek` is odd — e.g. English Language at 5 periods/week, max 2/day — which
  is common in this dataset) while still forbidding a non-adjacent same-day double and any
  placement that straddles a break. An earlier version of this rule forced `x[period1] ==
  x[period2]` for every pair-group unconditionally, which made every odd weekly total
  mathematically unsatisfiable and produced a false `INFEASIBLE`; the pairwise-forbid form
  above is the corrected implementation.
- **L1 hard-with-bounded-relaxation**: `scheduling-rules.json` marks L1 (same-first-word
  subjects can't share a day) as hard, not soft. For most sections that's directly satisfiable.
  But Class 12's "English Language" (5 ppw, max 2/day) and "English Literature" (7 ppw, max
  2/day) need at least `ceil(5/2) + ceil(7/2) = 7` distinct days between them, and only 6
  working days exist — this is a documented data/rule conflict
  (`TIMETABLE_GENERATION_DATA_REQUIREMENTS.md` §8.3), not a modeling mistake. Rather than fail
  the whole solve, `SameFirstWordSameDayBanRule` computes the exact unavoidable overflow per
  section/group and allows only that many same-day violations (mirroring the production
  engine's own force-fill Pass A2, which relaxes L1 as a last resort — see
  `Timetable-Engine-Rules.md` §14). The exact reasoning is reported back as a rule note (see
  `IConstraintRule.Apply`'s return value), which shows up in `schedulingConflicts` even on a
  **successful** solve.
- **"Used today" indicators** (L1, G2) use `CpModel.AddMaxEquality` to get an exact boolean
  reification of "any period of this item is used on this day", then apply `AddAtMostOne` /
  a `<= 1` sum across the group. This avoids the classic bug of an indicator that's merely an
  upper bound (which can silently over-constrain the model).
- **Infeasibility diagnosis**: OR-Tools doesn't produce a human-readable "why" for
  `INFEASIBLE`. `OrToolsTimetableGenerationService.DiagnoseTeacherOverload` checks the single
  highest-signal cause — any teacher whose total assigned weekly load exceeds the bell-schedule
  capacity — and reports each as a `DataConflictType.TeacherOverload` entry (not just a string),
  so the response stays structured and actionable. A full IIS/conflict-set computation was out
  of scope for the time available; when no teacher is individually overloaded, a generic
  "check overlapping constraints" note is returned instead.

---

## 9. What this project does

Given a school's sections, curriculum, teacher assignments, and bell schedule, this service
builds a **constraint-satisfaction model** of "who teaches what, to which section, in which
day/period slot" and asks Google OR-Tools CP-SAT to solve it. Concretely, a single run:

1. **Loads and normalizes** the raw dataset (JSON sections/bell-schedule, Markdown curriculum/
   teacher tables, or a self-contained sample file) into one consistent `SchoolModel`, flagging
   every data-quality problem it finds along the way instead of silently dropping rows.
2. **Builds a CP-SAT model**: one boolean decision variable per (section, curriculum item, day,
   period) that could legally hold a lesson, plus one `IConstraintRule` per hard rule from the
   assessment's rule spec (no teacher double-booking, one lesson per slot, daily/weekly period
   caps, block-subject pairing, Games/Library placement rules, same-first-word-same-day ban,
   etc.) and one soft objective term (prefer Mathematics in the morning).
3. **Solves** the model within a configurable time budget and maps the boolean solution back
   into a real per-section, per-day, per-period timetable.
4. **Reports** the outcome as structured JSON: the solver status, every scheduled lesson (by
   section and by teacher), every data conflict found while loading, and — if the model was
   infeasible — a clear, structured explanation of why, not just "INFEASIBLE".

All of this is exposed over a small REST API (see §3) so it can be driven from Swagger, a
frontend, or a test script without needing to know anything about CP-SAT internals.

---

## 10. Tools & technology stack

| Concern | Choice | Why |
|---|---|---|
| Runtime / framework | .NET 8, ASP.NET Core Web API | Required by the assessment; minimal hosting model via `Program.cs` |
| Constraint solver | `Google.OrTools` (CP-SAT), v9.10 | Required by the assessment; the only engine actually building/solving the timetable |
| API docs | `Swashbuckle.AspNetCore` | Serves interactive Swagger UI at `/swagger` for manually driving the endpoints |
| Data parsing | `System.Text.Json` (built-in) + a small hand-written `MarkdownTableParser` | No database and no Excel file were required for the modes this build supports, so no ORM or `ClosedXML` dependency was added — see §5 for the one place that would change if the xlsx became available |
| Testing | `xUnit`, `FluentAssertions` | Parser tests assert on real rows from the provided Markdown files; one CP-SAT smoke test runs an actual solve end-to-end against `school-sample.json` |
| Dependency injection | Built-in `Microsoft.Extensions.*` (`Options`, `Logging`, `Configuration`) | No third-party container needed — each project's `ServiceCollectionExtensions` wires its own layer |
| Data storage | None — file-based input, in-memory result cache (`ITimetableStateStore`) | Matches the assessment's "persistence not required" scope; see §11 |

No database, message queue, or external service is required to run this project — `dotnet run`
against the files already in `src/TimetableSolver.Api/Data/` is the entire runtime dependency
graph.

---

## 11. Scope — what's included and what's intentionally out

**Included and enforced in CP-SAT:** every Tier-1 hard rule and Tier-2 block/layout rule from
the assessment brief (see the Rule-ID map in §7), the soft Mathematics-in-the-morning
preference, and full data-conflict reporting for unassigned/zero-workload/ambiguous/missing
teacher assignments and missing curriculum.

**Intentionally not implemented**, each because the corresponding input data is genuinely empty
or absent in the provided package (not skipped for convenience):

- **Rooms (R1/R2)** — every provided room-related file/section is empty (no `room_type_id`, no
  active labs) — there is nothing to schedule against.
- **Section-level xlsx assignments** — see §5. This is the one gap that actually changes the
  full 41-section result (two teachers end up nominally responsible for more periods/week than
  exist, because the class-level file assigns them to *every* section of a grade at once).
  Plugging in the real `TEACHER_CLASS_ALLOCATION.xlsx` is the single change that would remove
  this limitation.
- **Persistence** — results live in `ITimetableStateStore` (in-memory singleton) for the
  lifetime of the process, matching "not required" in the assessment brief. Swapping in
  `timetable_versions` / `weekly_timetables` tables would only touch this one interface.
- **Substitution engine (SE\*/SA\*)** — explicitly deferred per the brief.
