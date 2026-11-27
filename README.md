# Adria MO

**Citizen project ownership for a social-housing building.** Residents photograph and date the defects of the common areas
(unfinished works, damage, recurring breakdowns), follow the works, estimate what repeated repairs cost, and report to the landlord.
Every tenant can see where things stand on a public page.

> Adria MO = *Adria maîtrise d'ouvrage*. The product is described in French in [`docs/spec.md`](docs/spec.md).

## How it is built

| Part | What | Where it runs |
|---|---|---|
| Public site (`web/`) | Astro + Tailwind, static, mobile first. Reads only a published snapshot of public data, so it stays up when the back end is down | A CDN |
| Back end (`src/`) | .NET 10 minimal API: administration, accounts and roles, photos, publication of the snapshot, scheduled e-mails | One container, with a data volume |

This repository holds **code only**. No data, photo, address or secret is ever committed: they live in the database and the
volume of each installation.

## Run it locally

Requirements: .NET SDK 10, Node.js 22.23 or later, Yarn 1.

```bash
# Back end on http://localhost:5080 (health: /health/live, info: /api/v1/info)
dotnet run --project src/AdriaMo.Api

# Public site on http://localhost:4321
cd web && yarn install && yarn dev
```

## Tests and checks

```bash
dotnet build src/AdriaMo.slnx -c Release          # warnings are errors, SonarQube rules included
dotnet test --solution src/AdriaMo.slnx -c Release
dotnet format src/AdriaMo.slnx --verify-no-changes

cd web
yarn lint && yarn format:check && yarn check       # ESLint (SonarJS, a11y), Prettier, astro check
yarn test                                          # Vitest
yarn build && yarn e2e                             # Playwright, phone-sized browser
```

The CI runs all of this, plus SonarQube, CodeQL, gitleaks, Trivy and the AI review agents; mutation testing (Stryker) runs weekly.
See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE).
