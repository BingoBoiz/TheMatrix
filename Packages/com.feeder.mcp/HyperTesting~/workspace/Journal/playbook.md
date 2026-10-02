# Play-test playbook

The orchestrator reads this before every Hyper Testing session and updates it after.

## Best known flow

1. Read this file and the last entries of the other journal files.
2. Original compiles clean.
3. `hyper-testing` `status` -> `up` (count from the safe clone count in `ram-observations.md`, default 2)
   -> poll `status` until ready.
4. `refresh` -> every clone `compiled clean`.
5. Write and split test cases; spawn all `hyper-playtester-N` in one message.
6. Poll `status` every few minutes while they run; watch RAM.
7. Summary, fixes in the original, `refresh`, re-run only failures.
8. Journal, `down`.

## Timings measured

| Date | Clones | New clone prepare | First open | Refresh+compile per clone | Cases | Wall time | One-by-one estimate |
|---|---|---|---|---|---|---|---|

## Tips proven to work

-

## Candidates (not yet proven - playtesters add here)

-
