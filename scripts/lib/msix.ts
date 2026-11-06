// The MSIX package (Microsoft Store): identity checks, the package version and the manifest.
// Used by scripts/msix-stage.mjs (which windows/packaging/msix/build-msix.ps1 runs) and tests/msix.test.ts.

export interface MsixIdentity {
  placeholder?: boolean;
  Name: string;
  Publisher: string;
  PublisherDisplayName: string;
  PackageFamilyName?: string;
  StoreId?: string;
}

/** Test values the repository may carry instead of the Partner Center identity. */
export const TEST_PUBLISHER = "CN=InstaWords-Test";

/** The files of the app's build output that go into the package (Licenses/ is copied as a folder). */
export const APP_FILES = ["InstaWords.exe", "InstaWords.exe.config", "InstaWords.Core.dll"];

/** "2.1.0", "v2.1.0", "2.2.0-beta.1" → "2.1.0.0", "2.2.0.0". The Store reserves the 4th part (must be 0). */
export function packageVersion(version: string): string {
  const m = /^v?(\d+)\.(\d+)\.(\d+)(?:[-+].*)?$/i.exec(String(version ?? "").trim());
  if (!m) throw new Error(`not a version: ${JSON.stringify(version)}`);
  const parts = [m[1], m[2], m[3]].map((p) => Number.parseInt(p ?? "", 10));
  if (parts.some((n) => n > 65535)) throw new Error(`version part above 65535: ${version}`);
  if (parts[0] === 0) throw new Error(`the Store needs a major version of 1 or more: ${version}`);
  return `${parts.join(".")}.0`;
}

/** Problems with identity.json (empty when it can be used to build a package). */
export function identityErrors(id: Partial<MsixIdentity> | null | undefined): string[] {
  const errors: string[] = [];
  if (!id || typeof id !== "object") return ["identity.json is not an object"];
  // Package/Identity/Name: 3-50 characters, letters, digits, "." and "-".
  if (typeof id.Name !== "string" || !/^[A-Za-z0-9.-]{3,50}$/.test(id.Name)) errors.push(`Name: 3-50 letters, digits, "." or "-" (got ${JSON.stringify(id.Name)})`);
  if (typeof id.Publisher !== "string" || !/^CN=[^"<>]{1,}$/.test(id.Publisher) || id.Publisher.length > 8192)
    errors.push(`Publisher: must start with "CN=" (got ${JSON.stringify(id.Publisher)})`);
  if (typeof id.PublisherDisplayName !== "string" || id.PublisherDisplayName.trim() === "" || id.PublisherDisplayName.length > 256)
    errors.push("PublisherDisplayName: required");
  if (id.PackageFamilyName !== undefined && !id.PackageFamilyName.startsWith(`${id.Name}_`))
    errors.push(`PackageFamilyName: must be "<Name>_<publisher hash>" (got ${JSON.stringify(id.PackageFamilyName)})`);
  return errors;
}

/** True for test values the Store would reject (placeholder flag, or the test publisher). */
export const isPlaceholder = (id: MsixIdentity): boolean => id.placeholder === true || id.Publisher === TEST_PUBLISHER;

const xmlEscape = (s: string): string =>
  s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;");

/** Fills {{Name}}, {{Publisher}}, {{PublisherDisplayName}} and {{Version}} in AppxManifest.xml. */
export function renderManifest(template: string, id: MsixIdentity, version: string): string {
  const values: Record<string, string> = {
    Name: id.Name,
    Publisher: id.Publisher,
    PublisherDisplayName: id.PublisherDisplayName,
    Version: packageVersion(version),
  };
  const out = template.replace(/\{\{(\w+)\}\}/g, (whole, key: string) => {
    const v = values[key];
    if (v === undefined) throw new Error(`unknown placeholder ${whole}`);
    return xmlEscape(v);
  });
  // The template's own comment lists the placeholders: only the attributes and elements must be filled.
  const body = out.replace(/<!--[\s\S]*?-->/g, "");
  if (/\{\{\w+\}\}/.test(body)) throw new Error("unfilled placeholder in the manifest");
  return out;
}

/** The file name of the Store package. */
export const packageFileName = (version: string): string => `InstaWords_${packageVersion(version)}_x64.msix`;
