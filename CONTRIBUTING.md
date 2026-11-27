# Contributing

Thanks for helping. Adria MO is small and meant to stay readable: a newcomer should be able to run it and change it in an afternoon.

## Branches (git flow)

- `develop` is the integration branch and the default one. Every change is a pull request into `develop`, merged by squash.
- `main` is production; only the release pull request (`develop` into `main`) targets it.
- Work on a branch named `feat/...`, `fix/...`, `docs/...` or `chore/...`.

Branches are protected: no direct push, the CI must be green.

## Pull requests

- **The title is a Conventional Commit** (`feat(web): show the age of each constat`, `fix(api): ...`): it becomes the commit on
  `develop`. A check enforces it.
- One pull request = one change you can demo. Keep it small.
- Tests come with the code: a rule of the product without a test is not done.
- The review agents comment on the pull request (label `ai-review` to run them again). Each finding gets an answer in
  `docs/review-decisions/<pr>.md` ([how](docs/review-decisions/README.md)).

## Layout

| Folder | What |
|---|---|
| `src/AdriaMo.Core` | The rules of the product, plain C# (no ASP.NET, no storage) |
| `src/AdriaMo.Api` | The HTTP host: endpoints, configuration (`ADRIAMO__*`), storage |
| `src/*.Test` | xUnit v3 tests; integration tests carry `[Trait("Category", "Integration")]` |
| `web/` | The public site (Astro + Tailwind), its unit tests (`*.test.ts`) and end-to-end tests (`e2e/`) |
| `docs/` | The specification, decisions and review decisions |

## Conventions

- English in code, comments, commits and technical docs; French in the user interface and in `docs/spec.md`.
- The build treats warnings as errors and runs the SonarQube rules: fix them, do not silence them without a written reason.
- Front end: Tailwind theme tokens only (the theme of Adria Résidents), mobile first, accessible (labels, roles, contrast), and
  never a hard-coded `/`: the site may live under a path of another site.
- **Never commit data**: no real photo, address, name, e-mail or secret, not even in a test. Test data is invented.

## Running the AI review locally

The agents live in `.claude/agents/`; with Claude Code, the `review-change` skill runs them on your branch.
