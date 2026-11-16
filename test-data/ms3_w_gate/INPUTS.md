# The 36 inputs of W (package ms3_w_gate 8.2, 2026-11-16)

In the order of the model's columns. `inputs_detail.tsv` holds the same table with all numbers; `inputs.tsv` is the short list of package 7 (where the two differ on the place in the log, this file is the exact one).

**`inputs_from_log.py` is the executable form of these definitions.** It rebuilds the 35 raw values of every MS3 command from the three log tables of a run folder. On the log folders of the 17 recorded runs it reproduces the values the model was fitted on for all 18,266 MS3: no input differs as a 32-bit value, and W is identical.

Kinds of input:

- **direct**: the engine writes this value into scan_commands.tsv with the MS3 command, so it holds it when it plans.
- **parent result**: a result of the parent MS2 (scan_results.tsv or identification.tsv); known once the MS2 is processed, which is before the MS3 is planned.
- **method**: a setting of the method.
- **derived**: computed from two other inputs.
- **state at planning**: the state of the engine at the moment it plans the MS3; for the training data it was rebuilt from the time stamps of the log, so the engine has to compute the same quantity from its own state.

Five inputs are passed as log10(max(value, 1)): `log10_intensity` (from the raw `intensity`), `id_score`, `x_prec_intensity`, `x_ms1_plan`, `x_ms2_mass_count`. `z_ms3_per_res` = `z_ms3` / `L`. `frag_N` = 1 for an N-terminal fragment. Everything else is passed as it is. All values are 32-bit floats when they reach the trees.

Range = minimum, 5th percentile, median, 95th percentile and maximum of the model input (after the transform) over the 8,482 MS3 of the fit. Gain = share of the model's total gain.

## 0. `frag_N`

- **What:** 1 if the target fragment is N-terminal (a, b or c ion), 0 if C-terminal.
- **Raw value to give:** `frag_type`; transform: N -> 1, C -> 0.
- **In FLASHIda's log:** scan_commands.tsv: ion_type of the MS3 command.
- **Kind:** direct. The engine computes: ion type of the target fragment: a, b, c -> 1; x, y, z -> 0.
- **Range in the fit:** 0 / 0 / 1 / 1 / 1 (2 distinct values); missing: 0%.
- **Gain:** 0.5%, 31 splits.

## 1. `z_ms2`

- **What:** charge of the MS2 precursor (the proteoform ion).
- **Raw value to give:** `z_ms2`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: charge of the MS3 command, first ';'-group (stage 0); within the group the first ','-value, the anchor.
- **Kind:** direct. The engine computes: charge of the proteoform ion that was fragmented in the MS2; with co-isolation notches the anchor charge of stage 0.
- **Range in the fit:** 4 / 4 / 11 / 27 / 50 (44 distinct values); missing: 0%.
- **Gain:** 1.8%, 84 splits.

## 2. `z_ms3`

- **What:** charge of the fragment that is isolated for the MS3.
- **Raw value to give:** `z_ms3`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: charge of the MS3 command, last ';'-group (stage 1); within the group the first ','-value, the anchor.
- **Kind:** direct. The engine computes: charge of the fragment ion that will be isolated; with co-isolation notches the anchor charge of stage 1. The fit holds no MS3 with notches.
- **Range in the fit:** 1 / 1 / 6 / 10 / 27 (24 distinct values); missing: 0%.
- **Gain:** 1.8%, 74 splits.

## 3. `z_ms3_per_res`

- **What:** z_ms3 / L.
- **Raw value to give:** `(derived)`; transform: z_ms3 / L.
- **In FLASHIda's log:** derived.
- **Kind:** derived. The engine computes: z_ms3 / L.
- **Range in the fit:** 0.0532 / 0.0769 / 0.118 / 0.2 / 0.5 (414 distinct values); missing: 0%.
- **Gain:** 2.3%, 115 splits.

## 4. `ce1`

- **What:** collision energy of the first stage (HCD), in % NCE.
- **Raw value to give:** `ce1`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: collision_energy of the MS3 command, first of the ';'-separated values.
- **Kind:** method. The engine computes: collision energy of the MS2 (HCD, % NCE).
- **Range in the fit:** 29 / 29 / 29 / 29 / 29 (1 distinct values); missing: 0%.
- **Gain:** 0.0%, 0 splits.

## 5. `cid3`

- **What:** collision energy of the MS3 stage (CID), in % NCE.
- **Raw value to give:** `cid3`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: collision_energy of the MS3 command, last value.
- **Kind:** method. The engine computes: collision energy of the MS3 (CID, % NCE).
- **Range in the fit:** 25 / 25 / 35 / 35 / 35 (3 distinct values); missing: 0%.
- **Gain:** 0.0%, 6 splits.

## 6. `log10_intensity`

- **What:** intensity of the target fragment in the parent MS2.
- **Raw value to give:** `intensity`; transform: log10(max(value, 1)).
- **In FLASHIda's log:** scan_commands.tsv: precursor_intensity, stage 1.
- **Kind:** direct. The engine computes: deconvolved intensity of the target fragment in the parent MS2.
- **Range in the fit:** 2.66 / 3.01 / 4.01 / 5.11 / 6.77 (6,695 distinct values); missing: 0%.
- **Gain:** 21.4%, 253 splits.

## 7. `L`

- **What:** number of residues of the target fragment.
- **Raw value to give:** `L`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: ms3_proteoform (residues counted).
- **Kind:** direct. The engine computes: residues of the target fragment.
- **Range in the fit:** 2 / 9 / 46 / 93 / 212 (139 distinct values); missing: 0%.
- **Gain:** 2.3%, 113 splits.

## 8. `id_score`

- **What:** identification score of the parent MS2.
- **Raw value to give:** `id_score`; transform: log10(max(value, 1)).
- **In FLASHIda's log:** identification.tsv: identification_score of the first row of the parent MS2.
- **Kind:** parent result. The engine computes: identification score of the parent MS2 as first written for that scan.
- **Range in the fit:** 0 / 1.15 / 1.94 / 2.64 / 3.04 (2,668 distinct values); missing: 0%.
- **Gain:** 7.1%, 131 splits.

## 9. `iso_cos`

- **What:** isotope cosine of the fragment in the MS2.
- **Raw value to give:** `iso_cos`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: iso_cos, stage 1.
- **Kind:** direct. The engine computes: isotope cosine of the target fragment in the parent MS2.
- **Range in the fit:** 0.85 / 0.873 / 0.969 / 0.997 / 1 (6,417 distinct values); missing: 0%.
- **Gain:** 4.0%, 122 splits.

## 10. `qscore`

- **What:** Q score of the fragment in the MS2.
- **Raw value to give:** `qscore`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: qscore, stage 1.
- **Kind:** direct. The engine computes: Q score of the target fragment in the parent MS2.
- **Range in the fit:** 0.214 / 0.49 / 0.899 / 0.965 / 0.991 (6,624 distinct values); missing: 0%.
- **Gain:** 0.9%, 66 splits.

## 11. `snr`

- **What:** signal to noise of the fragment in the MS2.
- **Raw value to give:** `snr`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: snr, stage 1.
- **Kind:** direct. The engine computes: signal to noise of the target fragment in the parent MS2.
- **Range in the fit:** 0.000589 / 0.694 / 6.66 / 190 / 4.17e+04 (6,695 distinct values); missing: 0%.
- **Gain:** 1.1%, 85 splits.

## 12. `has_shift`

- **What:** 1 if the target fragment carries a mass shift, else 0.
- **Raw value to give:** `has_shift`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: ms3_proteoform, without its leading <...>, contains '['.
- **Kind:** direct. The engine computes: 1 if the proteoform string of the target fragment itself holds a mass shift in brackets, localized or over a range; the prefix of fixed modifications <...> does not count.
- **Range in the fit:** 0 / 0 / 0 / 1 / 1 (2 distinct values); missing: 0%.
- **Gain:** 1.6%, 50 splits.

## 13. `n_ms2_prior`

- **What:** MS2 identifications of the proteoform that were back when the MS3 was enqueued (1 if the fragment could not be placed on the parent's stretch).
- **Raw value to give:** `n_ms2_prior`; transform: as is.
- **In FLASHIda's log:** identification.tsv: MS2 rows with the parent's (protein, start_pos, end_pos); scan_results.tsv: their received_ts against the enqueue_ts of the MS3.
- **Kind:** state at planning. The engine computes: the proteoform is keyed by (protein, start_pos, end_pos) of the parent's MS2 identification, not by its string or its precursor. Count the MS2 identifications of the run with that key whose scan result was received strictly before this MS3 is enqueued; the parent is one of them. This holds if the target fragment is placed on the parent's stretch (its residues are the first L residues of the parent's proteoform for an a, b or c fragment, the last L otherwise); if not, the value is 1.
- **Range in the fit:** 1 / 1 / 1 / 10 / 53 (46 distinct values); missing: 0%.
- **Gain:** 0.7%, 39 splits.

## 14. `n_ms3_prior`

- **What:** MS3 results of the proteoform that were back when the MS3 was enqueued (0 if the fragment could not be placed).
- **Raw value to give:** `n_ms3_prior`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: received_ts of the earlier MS3 placed under the same (protein, start_pos, end_pos), against the enqueue_ts of the MS3.
- **Kind:** state at planning. The engine computes: the MS3 that were placed under the same key and whose result was received strictly before this MS3 is enqueued; the MS3 of one plan do not count each other; 0 if the fragment is not placed.
- **Range in the fit:** 0 / 0 / 0 / 19 / 69 (56 distinct values); missing: 0%.
- **Gain:** 2.9%, 93 splits.

## 15. `mass_ms2`

- **What:** monoisotopic mass of the MS2 precursor.
- **Raw value to give:** `mass_ms2`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: mono_mass, stage 0.
- **Kind:** direct. The engine computes: monoisotopic mass of the MS2 precursor.
- **Range in the fit:** 2e+03 / 3.17e+03 / 1.07e+04 / 2.36e+04 / 4.78e+04 (2,849 distinct values); missing: 0%.
- **Gain:** 2.3%, 116 splits.

## 16. `mass_ms3`

- **What:** monoisotopic mass of the target fragment.
- **Raw value to give:** `mass_ms3`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: mono_mass, stage 1.
- **Kind:** direct. The engine computes: monoisotopic mass of the target fragment.
- **Range in the fit:** 200 / 1.01e+03 / 5.16e+03 / 1.02e+04 / 2.35e+04 (6,096 distinct values); missing: 0%.
- **Gain:** 4.1%, 159 splits.

## 17. `x_prec_intensity`

- **What:** intensity of the MS2 precursor in the MS1.
- **Raw value to give:** `x_prec_intensity`; transform: log10(max(value, 1)).
- **In FLASHIda's log:** scan_commands.tsv: precursor_intensity, stage 0.
- **Kind:** direct. The engine computes: intensity of the MS2 precursor in the MS1.
- **Range in the fit:** 3.49 / 4.5 / 5.35 / 6.37 / 7.49 (2,920 distinct values); missing: 0%.
- **Gain:** 1.4%, 97 splits.

## 18. `x_prec_qscore`

- **What:** Q score of the MS2 precursor.
- **Raw value to give:** `x_prec_qscore`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: qscore, stage 0.
- **Kind:** direct. The engine computes: Q score of the MS2 precursor.
- **Range in the fit:** 0.2 / 0.331 / 0.879 / 0.942 / 0.98 (2,903 distinct values); missing: 0%.
- **Gain:** 1.2%, 80 splits.

## 19. `x_prec_iso_cos`

- **What:** isotope cosine of the MS2 precursor.
- **Raw value to give:** `x_prec_iso_cos`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: iso_cos, stage 0.
- **Kind:** direct. The engine computes: isotope cosine of the MS2 precursor.
- **Range in the fit:** 0.85 / 0.869 / 0.969 / 0.997 / 0.999 (2,877 distinct values); missing: 0%.
- **Gain:** 1.4%, 115 splits.

## 20. `x_prec_snr`

- **What:** signal to noise of the MS2 precursor.
- **Raw value to give:** `x_prec_snr`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: snr, stage 0.
- **Kind:** direct. The engine computes: signal to noise of the MS2 precursor.
- **Range in the fit:** 0.00675 / 0.444 / 4.95 / 21.7 / 1.5e+03 (2,921 distinct values); missing: 0%.
- **Gain:** 0.9%, 73 splits.

## 21. `x_ms1_plan`

- **What:** intensity of the precursor mass (10 ppm, or one isotope spacing off) in the last MS1 survey resolved before the MS3 was enqueued; 0 if absent.
- **Raw value to give:** `x_ms1_plan`; transform: log10(max(value, 1)).
- **In FLASHIda's log:** scan_results.tsv: deconv_masses and deconv_intensities of the MS1 row with the largest resolve_ts at or before the enqueue_ts of the MS3.
- **Kind:** state at planning. The engine computes: the last MS1 = the MS1 scan with the latest resolve time at or before the enqueue time, of any kind (the fit used every MS1 row of the log). Sum the intensities of each deconvolved mass over its charges; of the masses within 10 ppm of the precursor's monoisotopic mass, or of it plus or minus 1.00335, take the largest sum; 0 if there is none.
- **Range in the fit:** 0 / 0 / 5.9 / 7.37 / 8.37 (1,976 distinct values); missing: 0%.
- **Gain:** 1.4%, 92 splits.

## 22. `x_charge_snr`

- **What:** signal to noise of the isolated charge state of the fragment.
- **Raw value to give:** `x_charge_snr`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: charge_snr, stage 1.
- **Kind:** direct. The engine computes: signal to noise of the charge state that will be isolated.
- **Range in the fit:** 0.148 / 0.906 / 12 / 322 / 4.17e+04 (6,693 distinct values); missing: 0%.
- **Gain:** 1.2%, 84 splits.

## 23. `x_charge_cos`

- **What:** isotope cosine of the isolated charge state of the fragment.
- **Raw value to give:** `x_charge_cos`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: charge_cos, stage 1.
- **Kind:** direct. The engine computes: isotope cosine of that charge state.
- **Range in the fit:** 0.306 / 0.853 / 0.964 / 0.996 / 1 (6,477 distinct values); missing: 0%.
- **Gain:** 4.4%, 97 splits.

## 24. `x_charge_score`

- **What:** score of the isolated charge state of the fragment.
- **Raw value to give:** `x_charge_score`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: charge_score, stage 1.
- **Kind:** direct. The engine computes: score of that charge state.
- **Range in the fit:** 0.7 / 1 / 1 / 1 / 1 (147 distinct values); missing: 0%.
- **Gain:** 0.2%, 8 splits.

## 25. `x_ppm_error`

- **What:** mass error of the fragment, ppm.
- **Raw value to give:** `x_ppm_error`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: ppm_error, stage 1.
- **Kind:** direct. The engine computes: mass error of the target fragment against its theoretical mass, ppm.
- **Range in the fit:** 0 / 0.38 / 1.14 / 2.68 / 5.22 (6,647 distinct values); missing: 0%.
- **Gain:** 1.6%, 126 splits.

## 26. `x_ms2_mass_count`

- **What:** deconvolved masses of the parent MS2.
- **Raw value to give:** `x_ms2_mass_count`; transform: log10(max(value, 1)).
- **In FLASHIda's log:** scan_results.tsv: mass_count of the parent MS2.
- **Kind:** parent result. The engine computes: number of deconvolved masses of the parent MS2.
- **Range in the fit:** 0.699 / 0.845 / 1.45 / 1.93 / 2.31 (137 distinct values); missing: 0%.
- **Gain:** 5.5%, 115 splits.

## 27. `x_ms2_tic_coverage`

- **What:** share of the parent MS2's ion current that its masses explain.
- **Raw value to give:** `x_ms2_tic_coverage`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: tic_coverage of the parent MS2.
- **Kind:** parent result. The engine computes: share of the parent MS2's ion current that its masses explain.
- **Range in the fit:** 0.0052 / 0.0597 / 0.236 / 0.629 / 0.98 (2,923 distinct values); missing: 0%.
- **Gain:** 1.1%, 84 splits.

## 28. `x_ms2_precursor_share`

- **What:** share of the precursor in the parent MS2.
- **Raw value to give:** `x_ms2_precursor_share`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: precursor_share of the parent MS2.
- **Kind:** parent result. The engine computes: share of the precursor in the parent MS2.
- **Range in the fit:** 0 / 0.00938 / 0.0633 / 0.563 / 0.844 (2,922 distinct values); missing: 0%.
- **Gain:** 1.0%, 81 splits.

## 29. `x_ms2_fragment_count`

- **What:** fragments of the parent MS2.
- **Raw value to give:** `x_ms2_fragment_count`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: fragment_count of the parent MS2.
- **Kind:** parent result. The engine computes: fragments of the parent MS2.
- **Range in the fit:** 1 / 2 / 7 / 28 / 67 (58 distinct values); missing: 0%.
- **Gain:** 7.0%, 84 splits.

## 30. `x_ms2_tag_count`

- **What:** sequence tags of the parent MS2.
- **Raw value to give:** `x_ms2_tag_count`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: tag_count of the parent MS2.
- **Kind:** parent result. The engine computes: sequence tags of the parent MS2.
- **Range in the fit:** 1 / 3 / 43 / 355 / 1.74e+03 (384 distinct values); missing: 0%.
- **Gain:** 1.2%, 102 splits.

## 31. `x_ms2_ions`

- **What:** matched fragments of the parent's identification.
- **Raw value to give:** `x_ms2_ions`; transform: as is.
- **In FLASHIda's log:** identification.tsv: number of ';'-separated entries of ms2_fragments of the first row of the parent MS2 (an empty field counts as 1).
- **Kind:** parent result. The engine computes: matched fragments of the parent's identification: the number of entries of its fragment list; an empty list counts as 1, as in the fit.
- **Range in the fit:** 1 / 2 / 7 / 28 / 67 (58 distinct values); missing: 0%.
- **Gain:** 5.0%, 36 splits.

## 32. `x_queue_ahead`

- **What:** MS3 commands enqueued earlier and not yet dequeued when this MS3 is enqueued.
- **Raw value to give:** `x_queue_ahead`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv enqueue_ts and scan_results.tsv dequeue_ts of the MS3 commands.
- **Kind:** state at planning. The engine computes: MS3 commands enqueued strictly before this one and not yet dequeued at its enqueue time. MS3 only, no MS2. The MS3 of one plan share an enqueue time and do not count each other, so every MS3 of a plan has the same value.
- **Range in the fit:** 0 / 0 / 0 / 7 / 12 (13 distinct values); missing: 0%.
- **Gain:** 2.9%, 95 splits.

## 33. `x_rank`

- **What:** position of the MS3 in its plan (same parent MS2, same enqueue time); 1 = first.
- **Raw value to give:** `x_rank`; transform: as is.
- **In FLASHIda's log:** scan_commands.tsv: row order within parent_tracking_id and enqueue_ts.
- **Kind:** state at planning. The engine computes: 1-based position of this MS3 among the MS3 of its plan (same parent MS2, same enqueue time), in the order the engine writes the commands; that is the plan order, not an intensity sort. Only MS3 that are sent count: with a gate that refuses MS3, the rank is the number sent before it in the plan plus 1.
- **Range in the fit:** 1 / 1 / 2 / 7 / 9 (9 distinct values); missing: 0%.
- **Gain:** 2.3%, 91 splits.

## 34. `x_plan_age`

- **What:** seconds from the last resolved MS1 survey to the enqueue of the MS3.
- **Raw value to give:** `x_plan_age`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv resolve_ts of that MS1 row, scan_commands.tsv enqueue_ts of the MS3.
- **Kind:** state at planning. The engine computes: seconds from the resolve time of that last MS1 (as for x_ms1_plan) to the enqueue time of the MS3.
- **Range in the fit:** 0 / 0.118 / 1.67 / 7.92 / 28 (2,104 distinct values); missing: 0%.
- **Gain:** 1.7%, 142 splits.

## 35. `x_rt_frac`

- **What:** retention time of the parent MS2 divided by the last retention time of the run (the length of the run).
- **Raw value to give:** `x_rt_frac`; transform: as is.
- **In FLASHIda's log:** scan_results.tsv: rt of the parent MS2 and the largest rt of the run.
- **Kind:** state at planning. The engine computes: retention time of the parent MS2 divided by the length of the run; the training data used the last retention time of the finished run, the engine has to use the planned length of the method.
- **Range in the fit:** 0.0385 / 0.111 / 0.515 / 0.908 / 0.983 (2,927 distinct values); missing: 0%.
- **Gain:** 4.1%, 175 splits.
