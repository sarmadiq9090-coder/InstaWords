import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { compareVersions, identityErrors, isPlaceholder, packageFileName, packageVersion, renderManifest, storeVersion } from "../scripts/lib/msix.ts";
import type { MsixIdentity } from "../scripts/lib/msix.ts";

const MSIX = join(import.meta.dirname, "..", "windows", "packaging", "msix");
const identity = JSON.parse(readFileSync(join(MSIX, "identity.json"), "utf8")) as MsixIdentity;
const template = readFileSync(join(MSIX, "AppxManifest.xml"), "utf8");

describe("MSIX package (Microsoft Store)", () => {
  test("package version is x.y.z.0", () => {
    assert.equal(packageVersion("2.1.0"), "2.1.0.0");
    assert.equal(packageVersion("v2.10.3"), "2.10.3.0");
    assert.equal(packageVersion("2.2.0-beta.1"), "2.2.0.0");
    assert.equal(packageVersion("2.2.0+build.5"), "2.2.0.0");
    assert.equal(packageFileName("2.1.0"), "InstaWords_2.1.0.0_x64.msix");
    for (const bad of ["", "2.1", "2.1.0.4", "x.y.z", "0.9.0", "70000.0.0"]) assert.throws(() => packageVersion(bad), bad);
  });

  const pkgVersion = (JSON.parse(readFileSync(join(import.meta.dirname, "..", "package.json"), "utf8")) as { version: string }).version;

  test("the package.json version makes a valid package version", () => {
    assert.match(packageVersion(pkgVersion), /^\d+\.\d+\.\d+\.0$/);
  });

  test("version comparison", () => {
    assert.equal(compareVersions("2.2.1", "2.2.0"), 1);
    assert.equal(compareVersions("2.2.0", "2.2.1"), -1);
    assert.equal(compareVersions("2.10.0", "2.9.9"), 1);
    assert.equal(compareVersions("2.2.0", "v2.2.0"), 0);
    assert.equal(compareVersions("2.2.0-beta.1", "2.2.0"), 0);
  });

  test("storeVersion: the Store build may run ahead of package.json, never behind", () => {
    assert.equal(storeVersion({}, "2.2.0"), "2.2.0");
    assert.equal(storeVersion({ storeVersion: "" }, "2.2.0"), "2.2.0");
    assert.equal(storeVersion({ storeVersion: "2.2.1" }, "2.2.0"), "2.2.1");
    assert.equal(storeVersion({ storeVersion: "2.2.0" }, "2.2.0"), "2.2.0");
    assert.equal(storeVersion({ storeVersion: "2.3.0" }, "2.2.0-beta.1"), "2.3.0");
    assert.throws(() => storeVersion({ storeVersion: "2.1.9" }, "2.2.0"), /lower than package\.json/);
    for (const bad of ["2.2", "2.2.1.0", "v2.2.1", "2.2.1-beta", "0.1.0"]) assert.throws(() => storeVersion({ storeVersion: bad }, "1.0.0"), bad);
  });

  test("identity.json storeVersion is 2.2.1 (package 2.2.1.0) and not behind package.json", () => {
    // Raise storeVersion for every Store submission (Partner Center needs a higher package version each
    // time) and update this test with it; see docs/distribution/microsoft-store.md, "Version of each submission".
    assert.equal(identity.storeVersion, "2.2.1");
    const v = storeVersion(identity, pkgVersion);
    assert.equal(v, "2.2.1");
    assert.ok(compareVersions(v, pkgVersion) >= 0);
    assert.equal(packageVersion(v), "2.2.1.0");
    assert.equal(packageFileName(v), "InstaWords_2.2.1.0_x64.msix");
    assert.match(renderManifest(template, identity, v), /<Identity Name="FastKiwi\.InstaWords" [^>]*Version="2\.2\.1\.0"/);
  });

  test("identity.json is usable", () => {
    assert.deepEqual(identityErrors(identity), []);
  });

  test("identity.json holds the Partner Center identity", () => {
    assert.equal(isPlaceholder(identity), false);
    assert.equal(identity.Name, "FastKiwi.InstaWords");
    assert.match(identity.Publisher, /^CN=[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}$/);
    assert.equal(identity.Publisher, "CN=8EFD6949-D088-481F-886D-82300679C5E5");
    assert.equal(identity.PublisherDisplayName, "FastKiwi");
    assert.equal(identity.PackageFamilyName, "FastKiwi.InstaWords_sm1xjmj6v76yc");
    assert.equal(identity.StoreId, "9P62P4WQSQNK");
  });

  test("identity checks", () => {
    const ok: MsixIdentity = { Name: "FastKiwi.InstaWords", Publisher: "CN=ABC", PublisherDisplayName: "FastKiwi" };
    assert.deepEqual(identityErrors(ok), []);
    assert.equal(identityErrors({ ...ok, Name: "Insta Words" }).length, 1);
    assert.equal(identityErrors({ ...ok, Name: "ab" }).length, 1);
    assert.equal(identityErrors({ ...ok, Publisher: "FastKiwi" }).length, 1);
    assert.equal(identityErrors({ ...ok, PublisherDisplayName: " " }).length, 1);
    assert.equal(identityErrors({ ...ok, PackageFamilyName: "Other_abc" }).length, 1);
    assert.equal(isPlaceholder({ ...ok, Publisher: "CN=InstaWords-Test" }), true);
    assert.equal(isPlaceholder({ ...ok, placeholder: true }), true);
    assert.equal(isPlaceholder(ok), false);
  });

  test("the manifest is filled and declares what the Store needs", () => {
    const xml = renderManifest(template, identity, "2.1.0");
    assert.doesNotMatch(xml, /\{\{\w+\}\}/);
    assert.match(xml, /<Identity Name="FastKiwi\.InstaWords" Publisher="CN=8EFD6949-D088-481F-886D-82300679C5E5" Version="2\.1\.0\.0" ProcessorArchitecture="x64" \/>/);
    assert.match(xml, /<PublisherDisplayName>FastKiwi<\/PublisherDisplayName>/);
    assert.match(xml, /<TargetDeviceFamily Name="Windows\.Desktop" MinVersion="10\.0\.19041\.0"/);
    assert.match(xml, /<rescap:Capability Name="runFullTrust" \/>/);
    assert.match(xml, /<Application Id="App" Executable="InstaWords\.exe" EntryPoint="Windows\.FullTrustApplication">/);
    assert.match(xml, /<uap5:StartupTask TaskId="InstaWordsStartup" Enabled="false"/);
    assert.match(xml, /<Resource Language="en-US" \/>\s*<Resource Language="ar" \/>/);
  });

  test("the manifest escapes XML", () => {
    const xml = renderManifest(template, { Name: "A.B", Publisher: "CN=X, O=\"Q&A\"", PublisherDisplayName: "<Me>" }, "1.0.0");
    assert.match(xml, /Publisher="CN=X, O=&quot;Q&amp;A&quot;"/);
    assert.match(xml, /<PublisherDisplayName>&lt;Me&gt;<\/PublisherDisplayName>/);
  });

  test("both package languages name the app InstaWords (the reserved name)", () => {
    for (const lang of ["en-US", "ar"]) {
      const resw = readFileSync(join(MSIX, "Strings", lang, "resources.resw"), "utf8");
      assert.match(resw, /<data name="AppDisplayName" xml:space="preserve">\s*<value>InstaWords<\/value>/, lang);
      assert.match(resw, /<data name="AppDescription" xml:space="preserve">\s*<value>[^<]{20,}<\/value>/, lang);
    }
  });
});
