# Improvements

Record of the initial code-review findings for this project, and what's been done
about each. Kept as a durable log so future work doesn't rediscover the same
issues or accidentally re-introduce something that was already fixed on purpose.

## Status legend

- ✅ Done — merged to `main`
- ⏳ Open — not yet addressed
- 🚫 Rejected — considered and deliberately not done (with reason)

## Bugs

- ✅ **Output filename used current wall-clock month instead of the scraped
  `Quarter` constant.** Fixed before this log started (PR #2,
  `5f42952`). The `.docx` filename now derives the quarter label from the
  `Quarter` const that was actually scraped, not `DateTime.UtcNow.Month`.
- ✅ **`GetVerse` threw on malformed input.** It assumed the memory-verse
  string always contained a matching `(`...`)` pair and a closing `»`; if the
  scraped HTML/text didn't match that shape, `IndexOf` returned `-1` and the
  following `Substring` calls threw `ArgumentOutOfRangeException`, aborting the
  whole run partway through. Fixed in PR #3
  (`fix/get-verse-defensive-parsing`, merged as `951909e`) — now guards against
  missing/misordered `(`/`)` and a missing `»`, returning `(string.Empty,
  string.Empty)` instead of throwing. Also fixed a related pre-existing offset
  quirk (the `»` index was computed against the untrimmed string but sliced
  from the trimmed one).
- ✅ **Failed HTTP fetches were silently swallowed.** Every
  `!response.IsSuccessStatusCode` check in `GetHeaders` just did `continue`/
  `return` with no log line, so a transient failure could quietly drop a day
  or a whole lesson and a "successful" run could produce incomplete output
  with no trace. Fixed in PR #5 (`feat/log-failed-fetches`, merged as
  `90ee42c`) — each failure path now logs a `logger.Warning` with the request
  URL and status code before skipping.

## Dead code

- ✅ **`Question.Node` (`HtmlNode`) field, `HyperlinkExample()` demo method,
  and a stale commented-out block referencing `question.Node`.** All had zero
  live callers. Removed in PR #4 (`chore/remove-dead-code`, merged as
  `4da9abe`).
- 🚫 **`Extensions.cs` (`string.Change()`) and its `Humanizer` dependency.**
  Also has zero live callers today, and was flagged as removable in the
  initial review. **Deliberately left in place** — `AGENTS.md` documents that
  this helper may be intended for future formatting work, so it's being kept
  rather than deleted. Revisit only if that intent is confirmed stale.
- ℹ️ **`GetListOfBooks()` / dynamic `bible.com` API fetch of book names.**
  Present in the very first version of this file; by the time this log was
  started, it had already been replaced with a hardcoded
  `BooksOfBible` list (66 entries, Ukrainian name → USFM code) directly in
  `WebScrapping.cs`. Not part of the tracked PR work above — noted here only
  for context, since it changes how `GetBibleLink` sources book names.

## Refactoring

- ✅ **`CreateDocs` was one ~165-line method** repeating the same
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

- ⏳ **`dynamic` + Newtonsoft.Json throughout `GetHeaders`.** API responses
  are deserialized as `dynamic` instead of typed DTOs, so a shape change in
  the upstream API fails at runtime (`RuntimeBinderException`) instead of at
  compile time. Defining real response types and switching to
  `System.Text.Json` would remove this risk and the Newtonsoft dependency.
- ⏳ **A new `HttpClient` is created per call** (`GetHeaders`) instead of one
  shared/injected instance — the classic socket-exhaustion anti-pattern.
  Low risk at current request volume, but easy to fix.
- ⏳ **`Year`/`Quarter` are hardcoded consts** at the top of `WebScrapping.cs`
  — the only way to scrape a different quarter is to edit source and
  rebuild. Could be read from `args`/config instead.
- ⏳ **`GetBibleLink` hand-parses Bible references** via string splitting,
  with two hardcoded book-name corrections (`"Филм"` → `"Филимона"`,
  `"Мих"` → `"Міхея"`). Fragile if more books need similar special-casing;
  a lookup table would scale better and be independently testable.
- ⏳ **Blocking `.Wait()`/`.Result` in `Run()`** instead of an async `Main`.
  Low risk in this single-threaded console app, but a latent deadlock trap
  if this code is ever reused somewhere with a sync context.
- ⏳ **No retry/backoff for transient HTTP failures** (e.g. via Polly) and
  **no delay/throttling between the many sequential requests** to
  `adventech.io`.
- ⏳ **No automated tests.** The regex-heavy Ukrainian text-cleanup logic in
  `GetHeaders`/`CreateDocs` (verse-reference extraction, question-text
  trimming) is exactly the kind of fiddly logic that would benefit most from
  unit tests, ideally after extracting it into its own testable
  class/method.

## Process notes

All four completed items (PRs #3–#6) were implemented as separate branches/
PRs per unit of work rather than one combined PR, deliberately in parallel
against the same base commit (each in its own git worktree) since the user
opted for speed over avoiding potential conflicts. In practice none of the
four PRs conflicted with each other — they touched disjoint regions of
`WebScrapping.cs`/`Ss.cs`. #6 was merged last, after being manually
re-merged with the updated `main` to pick up #3–#5 first.
