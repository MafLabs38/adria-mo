interface ImportMetaEnv {
  /** Where the published snapshot is read from (default `<base>data/`). */
  readonly PUBLIC_ADRIAMO_DATA_URL?: string;
  /**
   * The administration's API (default `/api/v1`). It must be on the same origin as the administration's pages (spec §10.3):
   * the session cookie is `SameSite=Strict` and the API answers no cross-origin request.
   */
  readonly PUBLIC_ADRIAMO_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
