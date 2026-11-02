Attribute VB_Name = "TafqitLib"
'==============================================================================
' Tafqit - Arabic numbers to words (tafqit) for Excel.  MIT License.
' https://github.com/sarmadiq9090-coder/tafket
'
'@@HEADER@@
'
' Worksheet functions (see vba/README.md):
'   =TAFQIT(Number, [Unit], [HundredsForm], [Prefix], [Suffix], [Decimals])
'   =TAFQIT_CLEAN(Number)
'
' Port of the TypeScript core (src/engine.ts, src/normalize.ts, src/catalog.ts,
' src/index.ts); rules in docs/SPEC.md. Output must match the core character for
' character. All Arabic text is stored as UTF-16 code points (U("0627 ...")) and
' decoded at run time, so this file stays pure ASCII for the VBA editor.
'
' Notes
' - The module is named TafqitLib, not Tafqit: Excel returns #NAME? when a
'   worksheet function has the same name as its module.
' - Amounts are processed as text only; Long is used for 3-digit groups.
' - A numeric cell (Double) is converted with CDec, which keeps 15 significant
'   digits (what Excel displays). Enter long amounts as text for exactness.
' - On error the function returns the Arabic message of ERROR_MESSAGES in
'   src/errors.ts, without the "(detail)" suffix of the core.
' - Plain arrays only (no Scripting.Dictionary), so it also runs on Excel for Mac.
'
' Keep this code within the VBA subset understood by tests/vba/harness/vba.mjs.
'==============================================================================
Option Explicit

Private Const E_EMPTY As Long = 1
Private Const E_INVALID_FORMAT As Long = 2
Private Const E_NEGATIVE As Long = 3
Private Const E_TOO_LARGE As Long = 4
Private Const E_TOO_MANY_DECIMALS As Long = 5
Private Const E_FRACTION_NOT_SUPPORTED As Long = 6
Private Const E_UNKNOWN_UNIT As Long = 7
Private Const E_INVALID_OPTION As Long = 8

' Index of the scale forms in mScale(k, f).
Private Const SC_SINGULAR As Long = 0
Private Const SC_DUAL As Long = 1
Private Const SC_PLURAL As Long = 2
Private Const SC_ACCUSATIVE As Long = 3

' Fraction digits accepted with the "none" unit (engine.ts).
Private Const MAX_FRACTION_DIGITS_NONE As Long = 15

'@@CONSTANTS@@

Private mReady As Boolean
Private mErr As Long
Private mErrMsg(1 To 8) As String

' Lexicon (src/lexicon.ts). Gender index: 0 = m, 1 = f. Hundreds form: 0 = first, 1 = second.
Private mZero As String
Private mWaw As String
Private mPoint As String
Private mOnes(0 To 1, 0 To 10) As String
Private mTeens(0 To 1, 0 To 9) As String
Private mComp(0 To 1, 0 To 10) As String
Private mTens(0 To 9) As String
Private mHund(0 To 1, 0 To 9) As String
Private mHundDC(0 To 1) As String
Private mHfName(0 To 1) As String
Private mScale(1 To 4, 0 To 3) As String

' Unit catalog (data/units.json), units 1..UNIT_COUNT.
Private mUId() As String
Private mUHasForms() As Boolean
Private mUGender() As Long
Private mUSing() As String
Private mUDual() As String
Private mUPlur() As String
Private mUAcc() As String
Private mUOne() As String
Private mUHasSub() As Boolean
Private mUSubGender() As Long
Private mUSubSing() As String
Private mUSubDual() As String
Private mUSubPlur() As String
Private mUSubAcc() As String
Private mUSubOne() As String
Private mUSubDigits() As Long
' Lookup names (unitKey of id, code, label and aliases) -> unit index.
Private mKeyCount As Long
Private mKeyName() As String
Private mKeyUnit() As Long

'==============================================================================
' Public worksheet functions
'==============================================================================

' Number: a cell, text or number. Unit: id, code, label or alias (default "none").
' HundredsForm: the spelling of 100 (default, or "1"/"2"). Prefix / Suffix: free text.
' Decimals: "round" (default, half-up to the subunit) or "error".
Public Function TAFQIT(ByVal Number As Variant, Optional ByVal Unit As Variant = "none", _
        Optional ByVal HundredsForm As Variant = "", Optional ByVal Prefix As Variant = "", _
        Optional ByVal Suffix As Variant = "", Optional ByVal Decimals As Variant = "") As Variant
    Dim vNum As Variant, vUnit As Variant, vHf As Variant
    Dim vPre As Variant, vSuf As Variant, vDec As Variant
    Dim ui As Long, hf As Long, code As Long, roundIt As Boolean
    Dim txt As String, ip As String, fp As String, words As String, decText As String

    EnsureInit
    vNum = ArgValue(Number)
    vUnit = ArgValue(Unit)
    vHf = ArgValue(HundredsForm)
    vPre = ArgValue(Prefix)
    vSuf = ArgValue(Suffix)
    vDec = ArgValue(Decimals)
    If IsCellError(vNum) Then
        TAFQIT = vNum
        Exit Function
    End If
    If IsCellError(vUnit) Then
        TAFQIT = vUnit
        Exit Function
    End If
    If IsCellError(vHf) Then
        TAFQIT = vHf
        Exit Function
    End If
    If IsCellError(vPre) Then
        TAFQIT = vPre
        Exit Function
    End If
    If IsCellError(vSuf) Then
        TAFQIT = vSuf
        Exit Function
    End If
    If IsCellError(vDec) Then
        TAFQIT = vDec
        Exit Function
    End If

    ' Same order of checks as tafqit() in src/index.ts.
    ui = FindUnit(ArgText(vUnit))
    If ui = 0 Then
        TAFQIT = mErrMsg(E_UNKNOWN_UNIT)
        Exit Function
    End If
    hf = ResolveHundreds(ArgText(vHf))
    If hf < 0 Then
        TAFQIT = mErrMsg(E_INVALID_OPTION)
        Exit Function
    End If
    code = NumberText(vNum, txt)
    If code = 0 Then code = NormalizeNumber(txt, ip, fp)
    If code <> 0 Then
        TAFQIT = mErrMsg(code)
        Exit Function
    End If
    decText = LowerKey(JsTrim(ArgText(vDec)))
    If decText = "" Or decText = "round" Then
        roundIt = True
    ElseIf decText = "error" Then
        roundIt = False
    Else
        TAFQIT = mErrMsg(E_INVALID_OPTION)
        Exit Function
    End If

    words = Phrase(ui, ip, fp, hf, roundIt)
    If mErr <> 0 Then
        TAFQIT = mErrMsg(mErr)
        Exit Function
    End If
    TAFQIT = JoinParts(JoinParts(JsTrim(ArgText(vPre)), words, " "), JsTrim(ArgText(vSuf)), " ")
End Function

' The number after cleaning (SPEC section 1), e.g. "123.45" for Arabic-Indic input.
Public Function TAFQIT_CLEAN(ByVal Number As Variant) As Variant
    Dim vNum As Variant, code As Long, txt As String, ip As String, fp As String
    EnsureInit
    vNum = ArgValue(Number)
    If IsCellError(vNum) Then
        TAFQIT_CLEAN = vNum
        Exit Function
    End If
    code = NumberText(vNum, txt)
    If code = 0 Then code = NormalizeNumber(txt, ip, fp)
    If code <> 0 Then
        TAFQIT_CLEAN = mErrMsg(code)
    ElseIf fp = "" Then
        TAFQIT_CLEAN = ip
    Else
        TAFQIT_CLEAN = ip & "." & fp
    End If
End Function

'==============================================================================
' Arguments
'==============================================================================

' A cell reference arrives as a Range object: use the value of its first cell.
Private Function ArgValue(ByVal v As Variant) As Variant
    If IsObject(v) Then
        ArgValue = v.Cells(1, 1).Value2
    Else
        ArgValue = v
    End If
End Function

' True for cell errors such as #N/A (they are returned unchanged); False for missing arguments.
Private Function IsCellError(ByVal v As Variant) As Boolean
    If IsError(v) Then
        IsCellError = Not IsMissing(v)
    Else
        IsCellError = False
    End If
End Function

' Text of an option argument. Numbers are converted without the locale (Str, not CStr).
Private Function ArgText(ByVal v As Variant) As String
    Select Case VarType(v)
    Case vbString
        ArgText = v
    Case vbBoolean
        If v Then
            ArgText = "TRUE"
        Else
            ArgText = "FALSE"
        End If
    Case vbInteger, vbLong, vbSingle, vbDouble, vbCurrency, vbDecimal, vbByte, 20
        ' 20 = vbLongLong (64-bit Office only).
        ArgText = Trim(Str(CDec(v)))
    Case Else
        ' Empty cell, missing argument, Null.
        ArgText = ""
    End Select
End Function

' Text of the Number argument. Returns an error code (0 = ok).
Private Function NumberText(ByVal v As Variant, ByRef txt As String) As Long
    txt = ""
    NumberText = 0
    Select Case VarType(v)
    Case vbString
        txt = v
    Case vbEmpty, vbNull
        txt = ""
    Case vbInteger, vbLong, vbSingle, vbDouble, vbCurrency, vbDecimal, vbByte, 20
        If v < 0 Then
            NumberText = E_NEGATIVE
        ElseIf v >= 1E+15 Then
            ' 16 or more integer digits (also keeps CDec from overflowing).
            NumberText = E_TOO_LARGE
        Else
            ' CDec keeps 15 significant digits of a Double; Str always uses "." as the
            ' decimal point (CStr would follow the regional settings).
            txt = Trim(Str(CDec(v)))
        End If
    Case Else
        ' Boolean, Date and anything else.
        NumberText = E_INVALID_FORMAT
    End Select
End Function

' 0 or 1 for the two hundreds forms (src/lexicon.ts HUNDREDS); -1 when invalid.
Private Function ResolveHundreds(ByVal s As String) As Long
    If s = "" Or s = "1" Then
        ResolveHundreds = 0
    ElseIf s = "2" Then
        ResolveHundreds = 1
    ElseIf s = mHfName(0) Then
        ResolveHundreds = 0
    ElseIf s = mHfName(1) Then
        ResolveHundreds = 1
    Else
        ResolveHundreds = -1
    End If
End Function

'==============================================================================
' Unit lookup (src/catalog.ts)
'==============================================================================

' Unit index, or 0 when unknown. An empty name means "none".
Private Function FindUnit(ByVal unitName As String) As Long
    Dim key As String, i As Long
    FindUnit = 0
    If unitName = "" Then unitName = "none"
    key = LowerKey(CollapseSpaces(unitName))
    For i = 1 To mKeyCount
        If mKeyName(i) = key Then
            FindUnit = mKeyUnit(i)
            Exit Function
        End If
    Next i
End Function

' name.trim().replace(/\s+/g, " ")
Private Function CollapseSpaces(ByVal s As String) As String
    Dim i As Long, ch As String, out As String, pending As Boolean
    out = ""
    pending = False
    For i = 1 To Len(s)
        ch = Mid(s, i, 1)
        If IsJsSpace(CharCode(ch)) Then
            If out <> "" Then pending = True
        Else
            If pending Then
                out = out & " "
                pending = False
            End If
            out = out & ch
        End If
    Next i
    CollapseSpaces = out
End Function

' toLowerCase() for the characters that can matter for the catalog keys.
Private Function LowerKey(ByVal s As String) As String
    Dim i As Long, out As String
    out = ""
    For i = 1 To Len(s)
        out = out & LowerChar(Mid(s, i, 1))
    Next i
    LowerKey = out
End Function

' String.prototype.trim()
Private Function JsTrim(ByVal s As String) As String
    Dim a As Long, b As Long
    a = 1
    b = Len(s)
    Do While a <= b
        If Not IsJsSpace(CharCode(Mid(s, a, 1))) Then Exit Do
        a = a + 1
    Loop
    Do While b >= a
        If Not IsJsSpace(CharCode(Mid(s, b, 1))) Then Exit Do
        b = b - 1
    Loop
    JsTrim = Mid(s, a, b - a + 1)
End Function

'==============================================================================
' Input cleaning (src/normalize.ts, SPEC section 1). Returns an error code (0 = ok).
'==============================================================================

Private Function NormalizeNumber(ByVal s As String, ByRef intPart As String, ByRef fracPart As String) As Long
    Dim i As Long, c As Long, t As String, kept As Long
    Dim dots As Long, digitCount As Long, dotPos As Long

    NormalizeNumber = 0
    intPart = ""
    fracPart = ""
    t = ""
    kept = 0
    For i = 1 To Len(s)
        c = CharCode(Mid(s, i, 1))
        Select Case c
        Case 32, 160, 8201, 8239, 9, 13, 10
            ' Spaces (U+0020, U+00A0, U+2009, U+202F, tab, CR, LF): removed.
        Case Else
            kept = kept + 1
            Select Case c
            Case 1632 To 1641
                ' Arabic-Indic digits U+0660..U+0669
                t = t & ChrW(c - 1584)
            Case 1776 To 1785
                ' Persian digits U+06F0..U+06F9
                t = t & ChrW(c - 1728)
            Case 44, 1644
                ' Thousands separators "," and U+066C: removed.
            Case 1643
                ' Arabic decimal separator U+066B
                t = t & "."
            Case Else
                t = t & Mid(s, i, 1)
            End Select
        End Select
    Next i
    If kept = 0 Then
        NormalizeNumber = E_EMPTY
        Exit Function
    End If
    If t <> "" Then
        c = CharCode(Left(t, 1))
        If c = 45 Or c = 8722 Then
            NormalizeNumber = E_NEGATIVE
            Exit Function
        End If
        If c = 43 Then t = Mid(t, 2)
    End If

    ' ^(\d+(\.\d*)?|\.\d+)$  =  only digits and at most one ".", with at least one digit.
    dots = 0
    digitCount = 0
    dotPos = 0
    For i = 1 To Len(t)
        c = CharCode(Mid(t, i, 1))
        If c >= 48 And c <= 57 Then
            digitCount = digitCount + 1
        ElseIf c = 46 Then
            dots = dots + 1
            dotPos = i
        Else
            NormalizeNumber = E_INVALID_FORMAT
            Exit Function
        End If
    Next i
    If dots > 1 Or digitCount = 0 Then
        NormalizeNumber = E_INVALID_FORMAT
        Exit Function
    End If

    If dotPos = 0 Then
        intPart = t
    Else
        intPart = Left(t, dotPos - 1)
        fracPart = Mid(t, dotPos + 1)
    End If
    intPart = StripLeadingZeros(intPart)
    fracPart = StripTrailingZeros(fracPart)
    If Len(intPart) > MAX_INTEGER_DIGITS Then
        NormalizeNumber = E_TOO_LARGE
        intPart = ""
        fracPart = ""
    End If
End Function

'==============================================================================
' Tafqit rules (src/engine.ts, SPEC sections 3-11)
'==============================================================================

' Full phrase without prefix/suffix. Sets mErr on error.
Private Function Phrase(ByVal ui As Long, ByVal intPart As String, ByVal fracPart As String, _
        ByVal hf As Long, ByVal roundIt As Boolean) As String
    Dim intText As String, subPart As String, subDigits As String, subText As String, d As Long

    mErr = 0
    Phrase = ""
    If Not mUHasForms(ui) Then
        intText = IntegerWords(intPart, 0, hf, False)
        If fracPart = "" Then
            Phrase = intText
        ElseIf Len(fracPart) > MAX_FRACTION_DIGITS_NONE Then
            mErr = E_TOO_MANY_DECIMALS
        Else
            Phrase = intText & " " & mPoint & " " & FractionWords(fracPart, hf)
        End If
        Exit Function
    End If

    subPart = "0"
    If fracPart <> "" Then
        If Not mUHasSub(ui) Then
            mErr = E_FRACTION_NOT_SUPPORTED
            Exit Function
        End If
        d = mUSubDigits(ui)
        If Len(fracPart) > d Then
            If Not roundIt Then
                mErr = E_TOO_MANY_DECIMALS
                Exit Function
            End If
            ' roundHalfUp: intPart & first d digits, plus one if the next digit is >= 5.
            subDigits = intPart & Left(fracPart, d)
            If Mid(fracPart, d + 1, 1) >= "5" Then subDigits = IncrementDigits(subDigits)
            intPart = StripLeadingZeros(Left(subDigits, Len(subDigits) - d))
            If Len(intPart) > MAX_INTEGER_DIGITS Then
                mErr = E_TOO_LARGE
                Exit Function
            End If
            subDigits = Right(subDigits, d)
        Else
            subDigits = fracPart & String(d - Len(fracPart), "0")
        End If
        subPart = StripLeadingZeros(subDigits)
    End If

    If subPart = "0" Then
        Phrase = CountPhrase(intPart, mUSing(ui), mUDual(ui), mUPlur(ui), mUAcc(ui), mUOne(ui), mUGender(ui), hf)
        Exit Function
    End If
    subText = CountPhrase(subPart, mUSubSing(ui), mUSubDual(ui), mUSubPlur(ui), mUSubAcc(ui), _
        mUSubOne(ui), mUSubGender(ui), hf)
    If intPart = "0" Then
        Phrase = subText
    Else
        Phrase = CountPhrase(intPart, mUSing(ui), mUDual(ui), mUPlur(ui), mUAcc(ui), mUOne(ui), mUGender(ui), hf) _
            & mWaw & subText
    End If
End Function

' SPEC section 5: number + counted noun. one = "" when the unit has no "one" form.
Private Function CountPhrase(ByVal digits As String, ByVal sing As String, ByVal dual As String, _
        ByVal plur As String, ByVal acc As String, ByVal one As String, ByVal gi As Long, _
        ByVal hf As Long) As String
    Dim r As Long, noun As String
    If digits = "0" Then
        CountPhrase = mZero & " " & sing
    ElseIf digits = "1" Then
        If one <> "" Then
            CountPhrase = one
        Else
            CountPhrase = sing & " " & mOnes(gi, 1)
        End If
    ElseIf digits = "2" Then
        CountPhrase = dual
    Else
        r = DigitsValue(Right(digits, 2))
        If r <= 2 Then
            noun = sing
        ElseIf r <= 10 Then
            noun = plur
        Else
            noun = acc
        End If
        CountPhrase = IntegerWords(digits, gi, hf, True) & " " & noun
    End If
End Function

' SPEC section 7: integer words without the counted noun.
Private Function IntegerWords(ByVal digits As String, ByVal gi As Long, ByVal hf As Long, _
        ByVal nounFollows As Boolean) As String
    Dim n As Long, k As Long, lowest As Long, p As Long, s As String
    Dim grp() As Long

    If digits = "0" Then
        IntegerWords = mZero
        Exit Function
    End If
    n = (Len(digits) + 2) \ 3
    ReDim grp(0 To n - 1)
    lowest = -1
    For k = 0 To n - 1
        p = Len(digits) - 3 * k - 2
        If p < 1 Then
            grp(k) = DigitsValue(Left(digits, Len(digits) - 3 * k))
        Else
            grp(k) = DigitsValue(Mid(digits, p, 3))
        End If
        If lowest = -1 And grp(k) > 0 Then lowest = k
    Next k

    s = ""
    For k = n - 1 To 1 Step -1
        If grp(k) > 0 Then
            ' The lowest non-zero scale is directly followed by the noun (SPEC section 6).
            s = JoinParts(s, ScaleTerm(grp(k), k, hf, (k = lowest) And nounFollows), mWaw)
        End If
    Next k
    If grp(0) > 0 Then s = JoinParts(s, GroupWords(grp(0), gi, hf, False), mWaw)
    IntegerWords = s
End Function

' SPEC sections 4 + 6: a scale group (thousands and up).
Private Function ScaleTerm(ByVal g As Long, ByVal k As Long, ByVal hf As Long, ByVal construct As Boolean) As String
    Dim r As Long, noun As String
    If g = 1 Then
        ScaleTerm = mScale(k, SC_SINGULAR)
        Exit Function
    End If
    If g = 2 Then
        ScaleTerm = mScale(k, SC_DUAL)
        Exit Function
    End If
    r = g Mod 100
    If r <= 2 Then
        noun = mScale(k, SC_SINGULAR)
    ElseIf r <= 10 Then
        noun = mScale(k, SC_PLURAL)
    ElseIf construct Then
        noun = mScale(k, SC_SINGULAR)
    Else
        noun = mScale(k, SC_ACCUSATIVE)
    End If
    ' Hundreds directly before a scale word are always in construct.
    ScaleTerm = GroupWords(g, 0, hf, True) & " " & noun
End Function

' SPEC section 3: words for 1..999.
Private Function GroupWords(ByVal g As Long, ByVal gi As Long, ByVal hf As Long, ByVal construct As Boolean) As String
    Dim h As Long, r As Long, un As Long, t As Long, hs As String, rs As String
    h = g \ 100
    r = g Mod 100
    hs = ""
    rs = ""
    If h > 0 Then
        If h = 2 And r = 0 And construct Then
            hs = mHundDC(hf)
        Else
            hs = mHund(hf, h)
        End If
    End If
    If r >= 1 And r <= 10 Then
        rs = mOnes(gi, r)
    ElseIf r >= 11 And r <= 19 Then
        rs = mTeens(gi, r - 10)
    ElseIf r >= 20 Then
        un = r Mod 10
        t = r \ 10
        If un = 0 Then
            rs = mTens(t)
        Else
            rs = mComp(gi, un) & mWaw & mTens(t)
        End If
    End If
    GroupWords = JoinParts(hs, rs, mWaw)
End Function

' SPEC section 10: fraction digits for the no-unit case; leading zeros read as zero words.
Private Function FractionWords(ByVal fraction As String, ByVal hf As Long) As String
    Dim lead As Long, i As Long, s As String
    lead = 0
    Do While lead < Len(fraction)
        If Mid(fraction, lead + 1, 1) <> "0" Then Exit Do
        lead = lead + 1
    Loop
    s = ""
    For i = 1 To lead
        s = JoinParts(s, mZero, " ")
    Next i
    If lead < Len(fraction) Then
        s = JoinParts(s, IntegerWords(Mid(fraction, lead + 1), 0, hf, False), " ")
    End If
    FractionWords = s
End Function

'==============================================================================
' String helpers
'==============================================================================

Private Function JoinParts(ByVal a As String, ByVal b As String, ByVal sep As String) As String
    If a = "" Then
        JoinParts = b
    ElseIf b = "" Then
        JoinParts = a
    Else
        JoinParts = a & sep & b
    End If
End Function

' Value of a string of 1..9 ASCII digits.
Private Function DigitsValue(ByVal s As String) As Long
    Dim i As Long, n As Long
    n = 0
    For i = 1 To Len(s)
        n = n * 10 + (CharCode(Mid(s, i, 1)) - 48)
    Next i
    DigitsValue = n
End Function

' Add one to a string of ASCII digits.
Private Function IncrementDigits(ByVal s As String) As String
    Dim i As Long, c As Long
    i = Len(s)
    Do While i >= 1
        c = CharCode(Mid(s, i, 1))
        If c <> 57 Then Exit Do
        s = Left(s, i - 1) & "0" & Mid(s, i + 1)
        i = i - 1
    Loop
    If i < 1 Then
        IncrementDigits = "1" & s
    Else
        IncrementDigits = Left(s, i - 1) & ChrW(c + 1) & Mid(s, i + 1)
    End If
End Function

Private Function StripLeadingZeros(ByVal s As String) As String
    Dim i As Long
    i = 1
    Do While i <= Len(s)
        If Mid(s, i, 1) <> "0" Then Exit Do
        i = i + 1
    Loop
    If i > Len(s) Then
        StripLeadingZeros = "0"
    Else
        StripLeadingZeros = Mid(s, i)
    End If
End Function

Private Function StripTrailingZeros(ByVal s As String) As String
    Dim i As Long
    i = Len(s)
    Do While i >= 1
        If Mid(s, i, 1) <> "0" Then Exit Do
        i = i - 1
    Loop
    StripTrailingZeros = Left(s, i)
End Function

' UTF-16 code unit of a character as 0..65535 (AscW is signed).
Private Function CharCode(ByVal ch As String) As Long
    Dim c As Long
    c = AscW(ch)
    If c < 0 Then c = c + 65536
    CharCode = c
End Function

' Decode "0623 0644 0641" (hex UTF-16 code units separated by spaces).
Private Function U(ByVal hexList As String) As String
    Dim i As Long, d As Long, v As Long, inToken As Boolean, out As String
    out = ""
    v = 0
    inToken = False
    For i = 1 To Len(hexList)
        d = InStr(1, "0123456789ABCDEF", UCase(Mid(hexList, i, 1)))
        If d > 0 Then
            v = v * 16 + (d - 1)
            inToken = True
        ElseIf inToken Then
            out = out & ChrW(v)
            v = 0
            inToken = False
        End If
    Next i
    If inToken Then out = out & ChrW(v)
    U = out
End Function

Private Sub EnsureInit()
    If mReady Then Exit Sub
    InitLexicon
    InitCatalog
    mReady = True
End Sub

'@@GENERATED@@
