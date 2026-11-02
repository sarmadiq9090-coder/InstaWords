// The generated .bas files must import cleanly into the (code-page based) VBA editor.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { BAS_FILES, vbaDir } from "./harness/load.mjs";

for (const file of BAS_FILES) {
  const buf = readFileSync(vbaDir + file);
  const text = buf.toString("latin1");

  test(`${file}: pure ASCII`, () => {
    const i = buf.findIndex((b) => b > 0x7e || (b < 0x20 && b !== 0x0d && b !== 0x0a && b !== 0x09));
    assert.equal(i, -1, `byte 0x${buf[i]?.toString(16)} at offset ${i}`);
  });

  test(`${file}: CRLF line endings only`, () => {
    assert.ok(text.endsWith("\r\n"));
    assert.doesNotMatch(text, /[^\r]\n/);
    assert.doesNotMatch(text, /\r(?!\n)/);
  });

  test(`${file}: VB_Name header and VBA line/procedure limits`, () => {
    const lines = text.split("\r\n");
    assert.match(lines[0], /^Attribute VB_Name = "[A-Za-z][A-Za-z0-9_]*"$/);
    for (const [n, l] of lines.entries()) assert.ok(l.length <= 1023, `line ${n + 1} is ${l.length} chars`);
    // VBA rejects procedures over 64 KB of compiled code; stay far below.
    let start = -1;
    for (const [n, l] of lines.entries()) {
      if (/^(Public |Private )?(Function|Sub) /.test(l)) start = n;
      if (/^End (Function|Sub)$/.test(l)) {
        assert.ok(n - start < 400, `procedure at line ${start + 1} has ${n - start} lines`);
        start = -1;
      }
    }
    // no more than 24 line continuations in one statement
    let cont = 0;
    for (const l of lines) {
      cont = / _$/.test(l) ? cont + 1 : 0;
      assert.ok(cont <= 24);
    }
  });
}

test("Tafqit.bas: module name differs from its worksheet functions", () => {
  const text = readFileSync(vbaDir + "Tafqit.bas", "latin1");
  const name = /^Attribute VB_Name = "([^"]+)"/.exec(text)[1];
  const publics = [...text.matchAll(/^Public (?:Function|Sub) (\w+)/gm)].map((m) => m[1].toLowerCase());
  assert.ok(publics.includes("tafqit") && publics.includes("tafqit_clean"));
  assert.ok(!publics.includes(name.toLowerCase()), "Excel shows #NAME? when a UDF has its module's name");
});
