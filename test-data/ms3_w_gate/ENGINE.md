# What the engine needs to evaluate W (package ms3_w_gate 8.2, 2026-11-16)

This is a specification, not code for FLASHIda. `w_gate.py` is a reference implementation in Python (standard library
only); the reference rows are the acceptance test.

## 1. When

Once for every MS3 the engine is about to plan (enqueue), before it decides to send it. One call needs the 36 inputs of
that MS3 and returns W. Cost: 84 trees, at most 6 comparisons each.

## 2. The 36 inputs

In the order of `inputs.tsv` (columns 0 to 35); `INPUTS.md` says for each one what it is and where the engine has it.

- 19 are values the engine already writes into `scan_commands.tsv` with the MS3 command.
- 7 are results of the parent MS2 that the engine already writes into `scan_results.tsv` or `identification.tsv`.
- 2 are method settings, 1 is derived (`z_ms3_per_res` = `z_ms3` / `L`).
- 7 describe the state of the engine at the moment of planning: `n_ms2_prior`, `n_ms3_prior`, `x_ms1_plan`, `x_queue_ahead`, `x_rank`, `x_plan_age`, `x_rt_frac`. For the training data they were rebuilt
  from the time stamps of the log. The engine should compute them from its own state by the definitions in `INPUTS.md`,
  and write them to the log (section 6), so that the two can be compared on the first run.

Every input must be present. None was ever missing in the recorded data; if the engine cannot fill one, it should not
evaluate W for that MS3 (and log that).

## 3. Transforms, in this order

1. `frag_N` = 1 if the target fragment is an a, b or c ion, 0 if it is an x, y or z ion.
2. `z_ms3_per_res` = `z_ms3` / `L`. `z_ms2` and `z_ms3` are the anchor charges of the two stages: with co-isolation
   notches the `charge` field of the log holds several charges per stage ("17,16,15;4,5"), and the first of each group
   is the one meant. No recorded run has notches, so W is not fitted for multiplexed MS3.
3. `log10_intensity` = log10(max(intensity, 1)).
4. For `id_score`, `x_prec_intensity`, `x_ms1_plan`, `x_ms2_mass_count`: round the value to a 32-bit float first, then
   log10(max(value, 1)).
5. Round all 36 values to 32-bit floats.

The logarithm may be taken in double precision and rounded to 32 bits afterwards; that is what `w_gate.py` does, and it
agrees with the training pipeline on all 18,266 recorded MS3 (largest difference of W 1.7e-07).

## 4. The trees

`trees.tsv` holds the 84 trees as rows: `tree`, `node`, `input_index`, `input_name`, `threshold`, `yes`, `no`,
`missing_goes_to_yes`, `leaf_value`. A row with a `leaf_value` is a leaf.

    sum = ln(b / (1 - b))                     with b = 0.4159396 (base_score of MANIFEST.json)
    for each tree:
        node = 0
        while the node is not a leaf:
            x = input[input_index]            (a 32-bit float)
            if x is missing:   node = yes if missing_goes_to_yes else no
            else if x < threshold:  node = yes       (threshold as a 32-bit float; strictly less)
            else:                   node = no
        sum = sum + leaf_value
    W = 1 / (1 + exp(-sum))

**Thresholds must be 32-bit floats.** The thresholds were placed at values of the training data, so an input is often
exactly equal to a threshold, and then the MS3 goes to `no`. `trees.tsv` writes every threshold out in full, so reading
it as a 32-bit float is exact. `w_ext_v3.json` prints the same thresholds in their shortest decimal form (1.5314789 for
1.5314788818359375); read as a 64-bit number and compared with a 32-bit input, that decimal sends such an MS3 the wrong
way. Of the 18,266 recorded MS3, 1,470 get a W that is off by more than 0.0001 from a reader that does this (largest
difference 0.14).
`reference_rows_ties.tsv` holds 200 of them, with the wrong W beside the right one.

The sum of the leaf values may be taken in double precision.

## 5. The gate

    send = (the MS3 has at least one uncovered bond) and (W >= threshold)

Two thresholds are held in `MANIFEST.json`: `w50` = 0.5 and `w10` = 0.835. Neither is chosen. The threshold should be a
setting of the method, written to the log with the run.

## 6. What to write to the log

For **every** MS3 the engine considers, whether it is sent or refused, one row with:

- W (at least six decimals), the identifier of the model (`w_ext_v3` and the first eight characters of its SHA-256,
  `47f0babe`), the threshold in use, the decision, and for a refusal the reason (W below the threshold, or no
  uncovered bond);
- the number of uncovered bonds of the target fragment at that moment;
- the 36 inputs as they were passed to the trees, or at least the 7 of the state at planning (`n_ms2_prior`, `n_ms3_prior`, `x_ms1_plan`, `x_queue_ahead`, `x_rank`, `x_plan_age`, `x_rt_frac`)
  and `id_score` as it stood;
- for a refused MS3 the same fields a sent MS3 has in `scan_commands.tsv` (tracking id, parent, target fragment and its
  proteoform string, masses, charges, intensities, time), so that it can enter the same tables.

Without the refused MS3 in the log, a gated run can only show that the sent MS3 behave as W says. With them it also
shows how many MS3 the gate sends and what W was for those it refused. Without the logged inputs, W recomputed offline
cannot be told apart from W as the engine computed it.

## 7. Acceptance test

Run all rows of `reference_rows.tsv` (300 MS3 of test proteins) and `reference_rows_ties.tsv` (200 MS3 where an input
equals a threshold) through the engine's implementation, from the raw values in the columns to W.

- W must agree with `w_expected` to 1e-6.
- The decisions must agree with `send_w50` and `send_w10` (given `n_uncovered_bonds`).

`python w_gate.py --selfcheck` does this for the reference implementation and also checks the hashes of the files. A
difference of the size of 0.01 or more on some rows and none on others is the signature of thresholds compared in the
wrong precision.

## 8. What is not needed

No XGBoost library and no Python at run time. `w_ext_v3.json` is in the package as the origin of the trees and for anyone
who wants to load it with XGBoost; `trees.tsv` is enough.

## 9. A reference for the inputs

`inputs_from_log.py` rebuilds the 35 raw inputs of every MS3 command of a run folder from `scan_commands.tsv`,
`scan_results.tsv` and `identification.tsv`. It is the executable definition of the inputs: on the log folders of the
17 recorded runs it reproduces the values the model was fitted on for all 18,266 MS3 (no input differs as a 32-bit
value; W identical). The values the engine computes from its own state can be compared with it on any run:

    python inputs_from_log.py <run folder> --out inputs.tsv --w

Where the engine's state and the rebuilt value can part, to be settled before the first gated run:

- `x_rank` and `x_queue_ahead` count MS3 that were sent (commands with a scan result). In the fit the MS3 of one plan
  shared an enqueue time: they all had the same `x_queue_ahead`, and `x_rank` ran 1..n in the order the engine wrote
  them. With a gate that refuses MS3, the rebuilt rank of an MS3 is the number sent before it in its plan plus 1, and a
  refused MS3 has no row to rebuild from; the engine's own value for it is all there is.
- `x_ms1_plan` and `x_plan_age` take the last MS1 of any kind. The recorded logs do not tell kinds of MS1 apart.
- `x_rt_frac` divides by the largest retention time of the finished run; the engine has the planned length.
- `n_ms2_prior` and `n_ms3_prior` use the time a scan result was received against the time the MS3 was enqueued.
