interface ImportMetaEnv {
  /** Where the published snapshot is read from (default `<base>data/`). */
  readonly PUBLIC_ADRIAMO_DATA_URL?: string;
  /** The administration's API (default `/api/v1`). */
  readonly PUBLIC_ADRIAMO_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
