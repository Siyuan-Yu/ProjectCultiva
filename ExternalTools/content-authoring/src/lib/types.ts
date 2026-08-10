export type JsonDict = Record<string, unknown>;

export interface ContentFile {
  path: string;
  schemaVersion: number;
  definitions: JsonDict[];
}

export interface DefRef {
  id: string;
  type: string;
  name: string;
  filePath: string;
  index: number;
  raw: JsonDict;
}

export interface PackageState {
  root: string;
  files: ContentFile[];
  defs: DefRef[];
  byId: Record<string, DefRef>;
}

export interface ValidationIssue {
  level: 'error' | 'warn';
  message: string;
  definitionId?: string;
  filePath?: string;
}

declare global {
  interface Window {
    studioApi?: {
      openPackageDialog: () => Promise<string | null>;
      defaultPackageRoot: () => Promise<string>;
      readText: (filePath: string) => Promise<string>;
      writeText: (filePath: string, text: string) => Promise<boolean>;
      listJsonFiles: (packageRoot: string) => Promise<string[]>;
      showItemInFolder: (filePath: string) => Promise<void>;
    };
  }
}
