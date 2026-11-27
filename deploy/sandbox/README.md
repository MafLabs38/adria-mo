# Sandbox

Two containers, as in production (spec §10.4): the back end, and an nginx standing for the CDN, which serves the built front,
the published snapshot (`/data/`) and the way to the API (`/api/`). Stop the back end: the public page stays online.

The session cookie is `Secure`, and the login page refuses to send a password over plain HTTP: the administration works over
HTTPS (a reverse proxy in front of the port), or on `localhost`; the public page works over plain HTTP too.

Nothing of the maintainer's machine is in this folder. A start script outside the repository sets:

| Variable | What |
|---|---|
| `ADRIAMO_STATE` | a folder outside the repository, holding `data/` and `publish/` (owned by user `1654`, the user of the image) and `super-admin-password` (readable by that user) |
| `ADRIAMO_SUPER_ADMIN` | the user name of the super-admin created at first start |
| `ADRIAMO_PORT` | the port of the static server (default `8490`) |
| `ADRIAMO_IMAGE` | the back-end image (default `ghcr.io/maflabs38/adria-mo:develop`) |
| `ADRIAMO_SITE` | the built front (default `../../web/dist`) |
| `ADRIAMO_TRUSTED_PROXY` | the address of the HTTPS proxy in front of the port, the only one whose `X-Forwarded-For` is believed (the login limit is per client address); default `127.0.0.1/32`, nothing trusted |

```bash
cd web && yarn build && cd ..
docker compose -f deploy/sandbox/compose.yaml up -d
docker compose -f deploy/sandbox/compose.yaml stop backend   # the public page stays online
```

## Trying it from a phone

The camera, the share target, "Ajouter à l'écran d'accueil" and the sending of photos later need a secure context: HTTPS with
a certificate the phone trusts (`http://localhost` counts as secure, so the e2e tests run on it). Put the sandbox behind your
HTTPS proxy, open its `admin/` page on the phone, or scan the pairing QR code shown on a computer: the code carries the address
the computer used, so open the administration on the computer through the same HTTPS address. `yarn dev` serves the service
worker too (`admin/sw.js`), but on `http://localhost` of the computer only.
