# API Reference (short)

Full-school CP-SAT timetable generator. Base URL locally: `http://localhost:5236` (default
`dotnet run` profile) — see `postman/` for a ready-to-use Postman collection + environment.

**Typical call order:** `Load Data` → `Generate` → any of the four report endpoints.

| Method | Endpoint | What it does |
|---|---|---|
| `GET` | `/health` | Liveness check. Returns `{status, timestampUtc}`. No dataset involved. |
| `POST` | `/api/timetable/load-data` | Loads + normalizes the configured dataset (Sample or FullDataset) into memory. Returns a summary (sections/teachers/curriculum counts) and any data conflicts found (unassigned teachers, zero-workload rows, missing curriculum, etc.). |
| `POST` | `/api/timetable/generate` | Builds the CP-SAT model from the loaded dataset (auto-loads if you skipped the step above) and solves it. Returns solver status + wall time, and on success the full timetable grouped by section and by teacher; on failure, a structured explanation of why. |
| `GET` | `/api/timetable/sections` | All section timetables from the last successful generation. `404` if nothing generated yet. |
| `GET` | `/api/timetable/sections/{sectionId}` | One section's timetable by its raw id (e.g. `class-1-lily`, not its display name). `404` if unknown or nothing generated yet. |
| `GET` | `/api/timetable/teachers` | Every teacher's combined weekly timetable across all sections they teach. `404` if nothing generated at all; `204` if the last generation was infeasible (see note below). |
| `GET` | `/api/timetable/conflicts` | Data conflicts (bad/missing input) + scheduling conflicts (why the solver failed, if it did). Safe to call any time. |

## Notes
- `DataSource:Mode` in `appsettings.json` picks the dataset: `FullDataset` (all 41 sections) or
  `Sample` (5 sections, resolves fastest — used by the default `Development` launch profile).
- `sectionId` must be a real id from `Data/sections.json` / `Data/school-sample.json`
  (`class-1-lily`, `class-8-lily`, …), not the human-readable display name.
- `GET /teachers` currently returns `204 No Content` (not an empty `200`) when the last
  `generate` call was `INFEASIBLE` — a minor existing inconsistency with `/sections`, which
  returns `200` with mostly-null fields in the same situation.

See `README.md` for architecture, data assumptions, and the full hard/soft rule mapping.
