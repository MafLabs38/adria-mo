interface ImportMetaEnv {
  /** Where the published snapshot is read from (default `<base>data/`). */
  readonly PUBLIC_ADRIAMO_DATA_URL?: string;
  /** The administration's API (default `/api/v1`: the administration lives at the root of its own address, spec §10.3). */
  readonly PUBLIC_ADRIAMO_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
