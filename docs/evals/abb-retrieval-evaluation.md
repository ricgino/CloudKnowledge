# ABB real-corpus retrieval evaluation

This evaluation is intentionally based on the real ABB manuals already used in the CloudKnowledge demo. It is not a unit-test replacement and it must not be used to tune one individual question until it passes.

Machine-readable cases are in `docs/evals/abb-retrieval-evaluation.json`.

## Goal

Measure whether the production retrieval pipeline can surface the correct evidence and generate a grounded answer across different failure modes:

- numeric and multi-hop reasoning;
- exact parameter identifiers;
- table extraction;
- explicit negations and safety limitations;
- revision/frame-specific requirements;
- no-answer / no-single-value questions;
- semantic + lexical fusion;
- navigation/index noise.

## Per-case scoring

For every case record:

1. **Evidence recall Top-5** — PASS only if at least one of the five final sources contains the required evidence itself. A table of contents, related-manual list, or cross-reference does not count.
2. **Answer correctness** — PASS only if all required facts/calculations are stated correctly.
3. **Grounding** — PASS only if the answer adds no unsupported numeric values, permissions, safety claims, or restrictions.
4. **Navigation noise** — count how many of the final five sources are table-of-contents, index, related-manual, or other navigation-like chunks.
5. **Evidence channel** — record whether the decisive selected evidence was Semantic, Lexical, or Both. This is diagnostic rather than pass/fail.

A case is considered **passed** when Evidence recall Top-5, Answer correctness, and Grounding all pass.

## Suggested acceptance target

Do not optimize from one case at a time. Run the whole catalog first and use failures to identify patterns.

Initial target for the demo corpus:

- at least 13/15 cases passed;
- 15/15 grounding passes;
- no invented value on ABB-11;
- no reversed/unsafe answer on ABB-05 or ABB-12;
- median navigation noise <= 1 source in the final Top-5.

The 13/15 retrieval target is intentionally less strict than grounding. A retriever can miss evidence; the answer generator must still refuse to invent unsupported facts.

## Run protocol

Use the same deployed build for all 15 questions.

For each question:

1. Select the same retrieval scope.
2. Ask the exact question from the JSON catalog without paraphrasing it during a benchmark run.
3. Open Retrieval diagnostics.
4. Save the Answer, final Sources used, and diagnostics.
5. Score the case before changing code.

Only after the full run is scored should a retrieval change be proposed. A fix should target a repeated failure pattern (for example navigation noise, exact-identifier recall, or frame-specific evidence), not an individual ABB question.

## Baseline control

ABB-01 is the previously investigated 3500 m altitude case. It now acts as a regression/control case rather than the benchmark's optimization target.

## Source documents used to author the ground truth

- `EN_ACS880-01_SynRM_SUPPL_C.pdf`
- `5929fb9d-7768-4176-9ddf-d1ff07b46875.pdf` — FSE-31 pulse encoder interface module user's manual
- `EN_ACS880-01-04-11-31-14-34_marine_SUPPL_F.pdf`

Some cases deliberately expect the answer to state that a source does **not** provide one universal value. Those cases are important hallucination-resistance tests and must not be converted into invented numeric ground truth.
