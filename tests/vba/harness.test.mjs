// Checks that the harness itself follows VBA semantics on small snippets.
import { test } from "node:test";
import assert from "node:assert/strict";
import { loadVba, VbaCompileError, VbaRuntimeError } from "./harness/vba.mjs";

const mod = (body) => loadVba([{ file: "T.bas", text: `Attribute VB_Name = "Snippet"\r\nOption Explicit\r\n${body}` }]);

test("string built-ins are 1-based and follow VBA rules", () => {
  const vm = mod(`
Public Function F(ByVal s As String) As String
    F = Mid(s, 2, 3) & "|" & Mid(s, 4) & "|" & Left(s, 2) & "|" & Right(s, 2) & "|" & InStr(1, s, "c") & "|" & InStr(s, "z") & "|" & Mid(s, 9)
End Function
Public Function A(ByVal s As String) As Long
    A = AscW(s)
End Function
Public Function T(ByVal s As String) As String
    T = "[" & Trim(s) & "]"
End Function`);
  assert.equal(vm.call("F", "abcdef"), "bcd|def|ab|ef|3|0|");
  assert.equal(vm.call("A", "﻿"), -257);
  assert.equal(vm.call("T", "\t a  "), "[\t a  ]");
  assert.throws(() => vm.call("A", ""), VbaRuntimeError);
});

test("integer arithmetic: \\, Mod, banker's rounding, overflow", () => {
  const vm = mod(`
Public Function D(ByVal a As Long, ByVal b As Long) As String
    D = (a \\ b) & " " & (a Mod b)
End Function
Public Function R(ByVal x As Double) As Long
    R = x
End Function
Public Function O() As Long
    O = 300 * 200
End Function`);
  assert.equal(vm.call("D", 7, 2), "3 1");
  assert.equal(vm.call("D", -7, 2), "-3 -1");
  assert.equal(vm.call("R", 2.5), 2);
  assert.equal(vm.call("R", 3.5), 4);
  assert.throws(() => vm.call("O"), /Overflow/); // Integer * Integer overflows in VBA
});

test("ByRef by default, ByVal copies, type mismatch is a compile error", () => {
  const vm = mod(`
Private Sub Bump(n As Long)
    n = n + 1
End Sub
Private Sub NoBump(ByVal n As Long)
    n = n + 1
End Sub
Public Function F() As Long
    Dim x As Long
    x = 1
    Bump x
    NoBump x
    Bump (x)
    F = x
End Function`);
  assert.equal(vm.call("F"), 2);
  assert.throws(() => mod(`
Private Sub S(ByRef t As String)
End Sub
Public Sub P()
    Dim n As Long
    S n
End Sub`), VbaCompileError);
});

test("Option Explicit, Select Case, loops, Exit, And is not short-circuit", () => {
  assert.throws(() => mod(`Public Function F() As Long\r\n    y = 1\r\nEnd Function`), /not defined/);
  const vm = mod(`
Public Function C(ByVal n As Long) As String
    Select Case n
    Case 1, 2
        C = "a"
    Case 3 To 10
        C = "b"
    Case Is >= 11
        C = "c"
    Case Else
        C = "d"
    End Select
End Function
Public Function L() As Long
    Dim i As Long, j As Long, s As Long
    For i = 1 To 10
        j = 0
        Do While True
            j = j + 1
            If j > 3 Then Exit Do
        Loop
        If i = 5 Then Exit For
        s = s + j
    Next i
    L = s * 100 + i
End Function
Public Function AndBoth(ByVal s As String) As Boolean
    AndBoth = Len(s) > 0 And AscW(s) = 65
End Function`);
  assert.deepEqual([1, 2, 5, 11, 0].map((n) => vm.call("C", n)), ["a", "a", "b", "c", "d"]);
  assert.equal(vm.call("L"), 1605);
  assert.throws(() => vm.call("AndBoth", ""), VbaRuntimeError); // VBA evaluates both sides of And
});

test("module and public function with the same name are rejected (#NAME? in Excel)", () => {
  assert.throws(() => loadVba([{ file: "X.bas", text: 'Attribute VB_Name = "Foo"\r\nOption Explicit\r\nPublic Function Foo() As Long\r\nEnd Function' }]), VbaCompileError);
});

test("a module array named like a built-in is rejected (it would shadow it)", () => {
  assert.throws(() => mod(`Private mId() As String`), /shadows/);
});
