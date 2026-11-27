# Adria MO — rules for the assistant

Adria MO is a citizen project-owner tool for one social-housing building (product: `docs/spec.md`, in French). Read it before
changing behaviour.

## Non-negotiable rules

1. **Public repository, no data.** Never commit a real photo, address, name, e-mail, domain of the maintainer's home network, token
   or password, not even in a test or a doc. Test data is invented. Secrets are read from mounted files or the environment.
2. **The public part never depends on the back end** (spec §10.2): public pages read only the published snapshot; nothing public
   calls the API. Only public data is published (no account, pending item, original photo or GPS).
3. **Integrable front** (spec §10.5): same stack and theme as Adria Résidents (Astro + Tailwind tokens of `web/src/styles/global.css`),
   mobile first, base path never hard-coded (`import.meta.env.BASE_URL`).
4. **Layers**: product rules in `AdriaMo.Core` (plain C#, no ASP.NET, no storage), endpoints thin in `AdriaMo.Api`, under `/api/v1`.
   Settings through configuration (`ADRIAMO__*`).
5. **Tests with the code**: a rule of the spec without a test is not done. Unit tests are fast (no real sleep: `FakeTimeProvider`);
   integration tests carry `[Trait("Category", "Integration")]`.
6. **Zero warning**: the build treats warnings as errors with the SonarQube rules; ESLint runs SonarJS. Fix, do not silence; a
   suppression carries its justification.
7. **One vertical = one pull request** into `develop`, with a Conventional Commit title, green CI and the review agents' findings
   answered in `docs/review-decisions/<pr>.md`.
8. **Files that judge a pull request are the maintainer's**: `.github/` (workflows, CODEOWNERS, approvers), `.claude/agents/`,
   `tools/review-decisions/`, `docs/review-decisions/README.md` and this file. The assistant may propose a change to them, never
   merge one on its own.
9. English in code, comments, commits and technical docs; French in the user interface and in the spec.

## Commands

```bash
dotnet build src/AdriaMo.slnx -c Release && dotnet test --solution src/AdriaMo.slnx -c Release
dotnet format src/AdriaMo.slnx --verify-no-changes
cd web && yarn lint && yarn format:check && yarn typecheck && yarn test && yarn build && yarn e2e
```

On the maintainer's small build machine, run heavy commands with `nice -n 15` and capped containers (`--cpus 2 --memory 3g`).
