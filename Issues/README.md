# Issues

Bugs found by testing (the in-game suite, Claude's reviews and charters, or by hand). One file per open issue; the
folder lists exactly what is open. Closed issues live on in git history.

## Workflow

1. **Found:** testing finds a bug → create `Issues/NNN-short-name.md` in a new file.
   - `NNN` is the next number never used before, also counting deleted issues:
     `git log --all --name-only --format= -- Issues | grep -o "[0-9]\{3\}-" | sort -u | tail -1`.
   - Contents: a table (status, found by, severity, requirement ID), summary, steps, expected, actual, evidence
     (log lines, test output), analysis, next steps. See an existing issue for the layout.
2. **Committed while open:** the issue file must appear in at least one commit before it is deleted, so its
   resolution can be tracked. Commit it on its own or with the test that shows the bug ("Add issue NNN: …").
   Update the file as the investigation goes on (status, findings).
3. **Resolved:** the commit that fixes the bug deletes the file. Its message names the issue
   ("Fix issue NNN: …") and says how it was resolved. The same applies to an issue that turns out to be intended
   behaviour or a test mistake ("Close issue NNN: …, intended"): the requirement or test is corrected in that commit.
4. **Tracking a closed issue:** `git log --all --full-history -- "Issues/NNN-*"` shows the commits that added,
   changed and deleted it; `git show <commit>^:Issues/NNN-short-name.md` shows its last text.
