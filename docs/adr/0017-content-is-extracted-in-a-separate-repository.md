# Extracting the syllabus lives in a separate repository

Lantern holds the code that is deployed and run: the API, the Functions app, the Parent app, the infrastructure, and the
tests and scripts that GitHub Actions and the local Docker stack run. How a Board's Books are turned into the content in
the `ncert` and `ssc` containers (downloading, text extraction, refinement, summaries, Class manifests, upload) is a
one-time operation that is not part of what runs, and it carries its own prompts and scripts. It now lives in the private
repository `lantern-utilities`, together with the one-time setup scripts (`github-setup.sh`, `setup-signing.sh`).

Lantern takes the content as given. The contract it must meet is `docs/architecture/contracts/README.md` and ADR-0014;
the container names are settings (D82). Nothing in Lantern describes how the content is made.

The split is a clean-up, not privacy: the git history of this repository keeps every moved file, and it was not rewritten.
A file moved only if a reference check found nothing in Lantern needing it (`docs/research/tooling-reference-check-181.md`);
`mkcert.sh`, `load-uat-masters.sh` and `token.sh` stayed because the Docker E2E, `verify-env.sh` and the local stack's
documented use need them, and `bootstrap.sh` stayed beside the `kv-param.sh` that CI needs.

Decisions about the content build (D5, D14 to D20, D53, D55 to D57, D69 to D71, D97, D101 to D103) are kept in
`lantern-utilities`. Their entries here stay as history.
