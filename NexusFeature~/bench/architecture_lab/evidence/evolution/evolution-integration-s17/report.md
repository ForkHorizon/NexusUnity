# Architecture lab report

Label: `EVOLUTION_INTEGRATION_ONLY`

This is a closed-loop integration run. It is not an architecture result.
These rows are not ranked.

| Candidate | Hash | Success | False success | Uncached/success | Calls | Wrong tools | Feasible |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SMOKE_OFFICIAL_SUBSET | `585a8eea710e` | 1 | 0 | 8455 | 3.5 | 1 | True |
| SMOKE_HIDDEN_GATEWAY | `a186319cc2d6` | 1 | 0 | 1.197e+04 | 2.5 | 0 | True |
| gen1_00003a7a | `00003a7ac5a8` | 1 | 0 | 1.019e+04 | 3 | 0 | True |

Raw token counts stay in the SQLite run database. Null costs are unpriced, not zero.

Stop: `STOP_GENERATION_LIMIT`

## Lineage

### Generation 0
- candidate `585a8eea710edb1c5bdba2496354d2b7fbed1a2709c393bb29200981dd672c65`
- candidate `a186319cc2d6c7e073b06049696625505631e708c190831cb9dd2fa5b86b2812`
- child `00003a7ac5a88750ec53d2515f7f7e9b55a98d53de210d8be5c3a3d8ac3abce0` parent `a186319cc2d6c7e073b06049696625505631e708c190831cb9dd2fa5b86b2812`

### Generation 1
- candidate `00003a7ac5a88750ec53d2515f7f7e9b55a98d53de210d8be5c3a3d8ac3abce0`

