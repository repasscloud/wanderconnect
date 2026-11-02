# Q98 / issue140 — residual Beta visual work

Item20 G–L uses existing CSS, not a new Bootstrap dependency. Email pre-authorisation has a useful desktop width and wraps with its action. Uniform filter controls use an adaptive grid; Clear filters has secondary control styling. Invitation/distribution first lines align with other columns by removing the first paragraph's default top margin. Server search, counts/paging, audited confirmed actions and provider status distinctions remain.

The new owned Beta assertions cover input width and rendered first-line alignment alongside existing 1,005-row functional checks. Initial attempt was blocked before the scenario by the concurrent model/snapshot pending-migration gate, not a visual-test failure. Rerun after integrated model completion is required. Q112 policy is separately available at /registration.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
