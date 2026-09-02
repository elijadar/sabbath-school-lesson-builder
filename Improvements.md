# Improvements

Record of the initial code-review findings for this project, and what's been done
about each. Kept as a durable log so future work doesn't rediscover the same
issues or accidentally re-introduce something that was already fixed on purpose.

## Status legend

- ✅ Done — merged to `main`
- ⏳ Open — not yet addressed
- 🚫 Rejected — considered and deliberately not done (with reason)

## Shape of the codebase

`Program.cs`, `WebScrapping.cs`, `Ss.cs`, `Extensions.cs` — a single console
app, .NET 10, ~440 LOC total. The flat file-per-concern layout is appropriate
for this size; a folder hierarchy (`/Services`, `/Models`, `/Infrastructure`)
or a DI container would be pure ceremony for 4 files and one linear run-once
pipeline. Same goes for the hardcoded `Year`/`Quarter`/URL constants and the
log-and-continue error handling — both are fine as-is for a tool the user
edits and reruns a few times a year. None of that is worth "fixing".

The one real structural problem is `WebScrapping.cs` (438 lines): a single
static class doing five distinct jobs — HTTP fetching, JSON parsing,
HTML/regex text extraction, Bible-reference resolution, and DOCX generation.
`GetHeaders` (WebScrapping.cs:105-233) mixes network calls, JSON traversal,
HTML scraping, and regex-based text cleanup in one 130-line method; `CreateDocs`
(WebScrapping.cs:263-369) mixes document generation with more regex-based text
cleanup that duplicates patterns already applied in `GetHeaders` (see
"Duplicate text-cleanup regex logic" below). Splitting into a `LessonFetcher`
(HTTP+JSON+HTML→`Ss`/`Day`/`Question` models) and a `DocumentBuilder`
(models→docx) would pay off now, mainly because it's the only way to unit
test the scraping/regex logic.

That ties into the bigger gap: almost none of the logic is unit-testable as
written. Everything is `private static` inside `WebScrapping`, so nothing can
be tested in isolation without reflection, and there's no test project in the
repo at all. The regex-based text-cleanup logic (WebScrapping.cs:184-206,
326-343) and `GetVerse`/`GetBibleLink` (WebScrapping.cs:235-261, 380-426) are
pure string-transformation logic with zero I/O dependency — exactly the
pieces that *should* be unit tested (fragile, regex-heavy, many textual edge
cases for Ukrainian Bible references) — but they're unreachable from outside
the class. Highest-value starting point: extract `GetVerse` and
`GetBibleLink` into a small public (or `internal` + `InternalsVisibleTo`)
static class and add a test project covering the book-name matching and
verse-reference parsing — no mocking needed, pure functions in, string out,
and they carry the highest bug-density risk (string index math, regex, a
hardcoded book-name lookup table with two special-cased abbreviations at
WebScrapping.cs:391-398).

## Bugs

1. ✅ **Output filename used current wall-clock month instead of the scraped
   `Quarter` constant.** Fixed before this log started (PR #2,
   `5f42952`). The `.docx` filename now derives the quarter label from the
   `Quarter` const that was actually scraped, not `DateTime.UtcNow.Month`.
2. ✅ **`GetVerse` threw on malformed input.** It assumed the memory-verse
   string always contained a matching `(`...`)` pair and a closing `»`; if the
   scraped HTML/text didn't match that shape, `IndexOf` returned `-1` and the
   following `Substring` calls threw `ArgumentOutOfRangeException`, aborting the
   whole run partway through. Fixed in PR #3
   (`fix/get-verse-defensive-parsing`, merged as `951909e`) — now guards against
   missing/misordered `(`/`)` and a missing `»`, returning `(string.Empty,
   string.Empty)` instead of throwing. Also fixed a related pre-existing offset
   quirk (the `»` index was computed against the untrimmed string but sliced
   from the trimmed one).
3. ✅ **Failed HTTP fetches were silently swallowed.** Every
   `!response.IsSuccessStatusCode` check in `GetHeaders` just did `continue`/
   `return` with no log line, so a transient failure could quietly drop a day
   or a whole lesson and a "successful" run could produce incomplete output
   with no trace. Fixed in PR #5 (`feat/log-failed-fetches`, merged as
   `90ee42c`) — each failure path now logs a `logger.Warning` with the request
   URL and status code before skipping.

## Dead code

1. ✅ **`Question.Node` (`HtmlNode`) field, `HyperlinkExample()` demo method,
   and a stale commented-out block referencing `question.Node`.** All had zero
   live callers. Removed in PR #4 (`chore/remove-dead-code`, merged as
   `4da9abe`).
2. 🚫 **`Extensions.cs` (`string.Change()`) and its `Humanizer` dependency.**
   Also has zero live callers today, and was flagged as removable in the
   initial review. **Deliberately left in place** — `AGENTS.md` documents that
   this helper may be intended for future formatting work, so it's being kept
   rather than deleted. Revisit only if that intent is confirmed stale.
3. ℹ️ **`GetListOfBooks()` / dynamic `bible.com` API fetch of book names.**
   Present in the very first version of this file; by the time this log was
   started, it had already been replaced with a hardcoded
   `BooksOfBible` list (66 entries, Ukrainian name → USFM code) directly in
   `WebScrapping.cs`. Not part of the tracked PR work above — noted here only
   for context, since it changes how `GetBibleLink` sources book names.

## Refactoring

1. ✅ **`CreateDocs` was one ~165-line method** repeating the same
   "create paragraph → set style → create run → set text" boilerplate over a
   dozen times. Partially addressed in PR #6
   (`refactor/extract-paragraph-helper`, merged as `99cee89`): added a private
   `AddParagraph(doc, style, text)` helper and replaced every simple
   static-text paragraph with a call to it (Title, Subtitle, section headings,
   arrow placeholders, per-day heading, etc.). The more complex sections —
   the memory-verse `IntenseQuote` paragraph (hyperlink run + a second
   `AddBreak`/`AppendText` run) and the per-question hyperlinked-verse
   paragraphs inside the day loop — were deliberately left as direct NPOI API
   calls, since forcing them through the helper would have made the
   hyperlink-building logic harder to follow. `CreateDocs` is meaningfully
   shorter (net -36 lines) but is still one large method; further splitting
   (e.g. extracting the per-day/per-question rendering into its own method)
   is still open if desired.

## Still open (not yet actioned)

1. ⏳ **`dynamic` + Newtonsoft.Json throughout `GetHeaders`.** API responses
   are deserialized as `dynamic` instead of typed DTOs, so a shape change in
   the upstream API fails at runtime (`RuntimeBinderException`) instead of at
   compile time. Defining real response types and switching to
   `System.Text.Json` would remove this risk and the Newtonsoft dependency.
2. ⏳ **A new `HttpClient` is created per call** (`GetHeaders`) instead of one
   shared/injected instance — the classic socket-exhaustion anti-pattern.
   Low risk at current request volume, but easy to fix.
3. ⏳ **`Year`/`Quarter` are hardcoded consts** at the top of `WebScrapping.cs`
   — the only way to scrape a different quarter is to edit source and
   rebuild. Could be read from `args`/config instead.
4. ⏳ **`GetBibleLink` hand-parses Bible references** via string splitting,
   with two hardcoded book-name corrections (`"Филм"` → `"Филимона"`,
   `"Мих"` → `"Міхея"`). Fragile if more books need similar special-casing;
   a lookup table would scale better and be independently testable.
5. ✅ **Blocking `.Wait()`/`.Result` in `Run()`** instead of an async `Main`.
   Fixed: `Main` is now `static async Task Main`, `WebScrapping.Run` returns
   `Task`, and both calls use `await`. See
   [issue #8](https://github.com/elijadar/sabbath-school-lesson-builder/issues/8).
6. ⏳ **No retry/backoff for transient HTTP failures** (e.g. via Polly) and
   **no delay/throttling between the many sequential requests** to
   `adventech.io`.
7. ⏳ **No automated tests.** The regex-heavy Ukrainian text-cleanup logic in
   `GetHeaders`/`CreateDocs` (verse-reference extraction, question-text
   trimming) is exactly the kind of fiddly logic that would benefit most from
   unit tests, ideally after extracting it into its own testable
   class/method.
8. ✅ **Duplicate text-cleanup regex logic in two places.** The pattern of
   stripping prefixes/punctuation from question text appeared both in
   `GetHeaders` (WebScrapping.cs:196-204) and again, differently, in
   `CreateDocs` (WebScrapping.cs:338-343), and had already diverged. Fixed:
   extracted `StripQuestionLeadIn` (used by `GetHeaders`) and
   `StripVerseRemnant` (used by `CreateDocs`) as named helpers. A single fully
   merged `CleanQuestionText` helper was tried first, but caused text-
   corruption regressions since the two original call sites clean text at
   different pipeline stages — kept as two behavior-preserving helpers
   instead. A handful of pre-existing text-cleanup artifacts (stray
   punctuation remnants like `? ?`/`; ; .`) were found during verification and
   confirmed present on `main` before this change too — out of scope here,
   left for a follow-up. See
   [issue #18](https://github.com/elijadar/sabbath-school-lesson-builder/issues/18).

## Process notes

All four completed items (PRs #3–#6) were implemented as separate branches/
PRs per unit of work rather than one combined PR, deliberately in parallel
against the same base commit (each in its own git worktree) since the user
opted for speed over avoiding potential conflicts. In practice none of the
four PRs conflicted with each other — they touched disjoint regions of
`WebScrapping.cs`/`Ss.cs`. #6 was merged last, after being manually
re-merged with the updated `main` to pick up #3–#5 first.

## What NOT to change

1. **The flat file structure itself.** No need for `/Models`, `/Services`,
   `/Infrastructure` folders — that would be over-engineering for a script
   this size.
2. **No DI container.** One linear run-once pipeline; adding
   `IServiceCollection`/`IHostBuilder` would be pure ceremony.
3. **Hardcoded `Year`/`Quarter`/URLs as constants** for the tool's own use
   (the config-vs-args question is tracked as an open item above, but the
   *hardcoding itself* isn't a problem for a tool the user edits and reruns a
   few times a year).
4. **Error-handling style (log + continue on failed fetch).** Already
   consistent and reasonable; don't add try/catch elsewhere for symmetry's
   sake.
5. **`Ss.cs` model classes.** Simple DTOs, no behavior — appropriately plain.
