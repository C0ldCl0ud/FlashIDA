# ms3_w_gate, version 8.2 (2026-11-16)

W of the MS3 gate, documented for building it into FLASHIda. W is the probability that the MS3 of a planned target
fragment shows at least one bond of that fragment. The model is `w_ext_v3.json` (SHA-256
`47f0babed8f470e242510765cfc3e49c35dcfce5d035efc149bf025a81ba425b`), W of model version 3, unchanged.

**Status: a fixed version of the model and its documentation. No threshold is chosen, no gate is released, nothing is
deployed.**

| Read this | For |
|---|---|
| `MODEL_CARD.md` | what W predicts, the label, the training data, how it was checked, where it holds and where not |
| `INPUTS.md`, `inputs_detail.tsv` | the 36 inputs: definition, place in FLASHIda's log, what the engine computes, transform, range in the fit |
| `ENGINE.md` | what the engine needs: the evaluation step by step, 32-bit thresholds, the gate, what to log, the acceptance test |
| `CHANGELOG.md` | what changed against package 7 |

| File | What |
|---|---|
| `w_ext_v3.json` | the model (XGBoost 3.4.1, binary:logistic, 84 trees, 36 inputs) |
| `trees.tsv` | the same trees as a flat table with the thresholds written out in full |
| `inputs.tsv` | the 36 inputs in the order of the model's columns (short list) |
| `reference_rows.tsv` | 300 MS3 with raw inputs, the W they must give, and the decision at each threshold |
| `reference_rows_ties.tsv` | 200 MS3 where an input equals a split threshold, with the W a 64-bit comparison would give |
| `w_gate.py` | reference implementation and self-check (Python standard library only) |
| `inputs_from_log.py` | the raw inputs of every MS3 command of a run folder, rebuilt from its log tables: the executable definition of the inputs |
| `MANIFEST.json` | version, thresholds, SHA-256 of every file |

    python w_gate.py --selfcheck                    # must end with "passed": true
    python w_gate.py --tsv in.tsv --out out.tsv     # W and both decisions for a table of candidates
    python inputs_from_log.py <run folder> --out inputs.tsv --w     # raw inputs and W for every MS3 of a recorded run

    import w_gate
    model = w_gate.Model()
    w = model.w(w_gate.model_inputs(raw))           # raw: dict of the 35 raw values of inputs.tsv
    send = w_gate.decide(w, n_uncovered_bonds, w_gate.variants()["w50"])
