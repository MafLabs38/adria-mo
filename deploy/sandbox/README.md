# Sandbox

Two containers, as in production (spec §10.4): the back end, and an nginx standing for the CDN, which serves the built front,
the published snapshot (`/data/`) and the way to the API (`/api/`). Stop the back end: the public page stays online.

Nothing of the maintainer's machine is in this folder. A start script outside the repository sets:

| Variable | What |
|---|---|
| `ADRIAMO_STATE` | a folder outside the repository, holding `data/` and `publish/` (owned by user `1654`, the user of the image) and `super-admin-password` (readable by that user) |
| `ADRIAMO_SUPER_ADMIN` | the user name of the super-admin created at first start |
| `ADRIAMO_PORT` | the port of the static server (default `8490`) |
| `ADRIAMO_IMAGE` | the back-end image (default `ghcr.io/maflabs38/adria-mo:develop`) |
| `ADRIAMO_SITE` | the built front (default `../../web/dist`) |
| `ADRIAMO_SECURE_COOKIE` | `false` by default here, as the sandbox is also reached by plain HTTP on the local network; never in production |

```bash
cd web && yarn build && cd ..
docker compose -f deploy/sandbox/compose.yaml up -d
docker compose -f deploy/sandbox/compose.yaml stop backend   # the public page stays online
```
