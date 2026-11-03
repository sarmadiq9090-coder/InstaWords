Attribute VB_Name = "TafqitLib"
'==============================================================================
' InstaWords (tafqit) - numbers to words in Arabic, English and French, for
' Excel. MIT License. https://github.com/sarmadiq9090-coder/tafket
'
'@@HEADER@@
'
' Worksheet functions (see vba/README.md):
'   =TAFQIT(Number, [Unit], [HundredsForm], [Prefix], [Suffix], [Decimals], [Language])
'   =TAFQIT_CLEAN(Number)
'
' Port of the TypeScript core (src/engine.ts, src/lang/*.ts, src/normalize.ts,
' src/catalog.ts, src/index.ts); rules in docs/SPEC.md. Output must match the
' core character for character. Text that is not printable ASCII is stored as
' UTF-16 code units (U("0627 ...")) and decoded at run time, so this file stays
' pure ASCII for the VBA editor.
'
' Notes
' - The module is named TafqitLib, not Tafqit: Excel returns #NAME? when a
'   worksheet function has the same name as its module.
' - Amounts are processed as text only; Long is used for 3-digit groups.
' - A numeric cell (Double) is converted with CDec, which keeps 15 significant
'   digits (what Excel displays). Enter long amounts as text for exactness.
' - On error the function returns the Arabic message of ERROR_MESSAGES in
'   src/errors.ts (in every language), without the "(detail)" suffix of the core.
' - Plain arrays only (no Scripting.Dictionary), so it also runs on Excel for Mac.
'
' Keep this code within the VBA subset understood by tests/vba/harness/vba.mjs.
'==============================================================================
Option Explicit

' Languages (src/lang/index.ts): index into the per-language arrays.
Private Const LANG_AR As Long = 0
Private Const LANG_EN As Long = 1
Private Const LANG_FR As Long = 2
Private Const LANG_COUNT As Long = 3

' Index of the Arabic scale forms in mScale(k, f).
Private Const SC_SINGULAR As Long = 0
Private Const SC_DUAL As Long = 1
Private Const SC_PLURAL As Long = 2
Private Const SC_ACCUSATIVE As Long = 3

' Fraction digits accepted with the "none" unit (engine.ts).
Private Const MAX_FRACTION_DIGITS_NONE As Long = 15

' Error codes E_* (src/errors.ts), MAX_INTEGER_DIGITS, UNIT_COUNT, KEY_COUNT:
'@@CONSTANTS@@

Private mReady As Boolean
Private mErr As Long
Private mErrMsg(1 To ERROR_COUNT) As String
Private mLangId(0 To 2) As String
Private mJoiner(0 To 2) As String

' Arabic (src/lexicon.ts). Gender index: 0 = m, 1 = f. Hundreds form: 0 = first, 1 = second.
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

' English (EN_WORDS in src/lang/en.ts).
Private mEnSmall(0 To 19) As String
Private mEnTens(0 To 9) As String
Private mEnScale(0 To 4) As String
Private mEnHundred As String
Private mEnPoint As String
Private mEnHyphen As String

' French (FR_WORDS in src/lang/fr.ts).
Private mFrSmall(0 To 19) As String
Private mFrTens(0 To 6) As String
Private mFrScale(0 To 4) As String
Private mFrFourTwenty As String
Private mFrCent As String
Private mFrMille As String
Private mFrPoint As String
Private mFrEt As String
Private mFrHyphen As String
Private mFrPluralS As String
Private mFrOneF As String
Private mFrDe As String
Private mFrDeElided As String

' Unit catalog (data/units.json), units 1..UNIT_COUNT. (unit, language) arrays hold the
' forms of each language: Arabic from "forms", English/French from "i18n".
Private mUId() As String
Private mUHasForms() As Boolean
Private mFHas() As Boolean
Private mFSing() As String
Private mFPlur() As String
Private mFOne() As String
Private mFGender() As Long
Private mFDual() As String
Private mFAcc() As String
' Subunit: same layout, plus the number of fraction digits it takes.
Private mUHasSub() As Boolean
Private mUSubDigits() As Long
Private mSHas() As Boolean
Private mSSing() As String
Private mSPlur() As String
Private mSOne() As String
Private mSGender() As Long
Private mSDual() As String
Private mSAcc() As String
' Lookup names (unitKey of id, code, label and aliases) -> unit index.
Private mKeyCount As Long
Private mKeyName() As String
Private mKeyUnit() As Long

'==============================================================================
' Public worksheet functions
'==============================================================================

' Number: a cell, text or number. Unit: id, code, label or alias (default "none").
' HundredsForm: the spelling of 100 in Arabic (default, or "1"/"2"). Prefix / Suffix: free text.
' Decimals: "round" (default, half-up to the subunit) or "error".
' Language: "ar" (default), "en" or "fr".
Public Function TAFQIT(ByVal Number As Variant, Optional ByVal Unit As Variant = "none", _
        Optional ByVal HundredsForm As Variant = "", Optional ByVal Prefix As Variant = "", _
        Optional ByVal Suffix As Variant = "", Optional ByVal Decimals As Variant = "", _
        Optional ByVal Language As Variant = "") As Variant
    Dim vNum As Variant, vUnit As Variant, vHf As Variant
    Dim vPre As Variant, vSuf As Variant, vDec As Variant, vLang As Variant
    Dim ui As Long, hf As Long, lang As Long, code As Long, roundIt As Boolean
    Dim txt As String, ip As String, fp As String, words As String, decText As String

    EnsureInit
    vNum = ArgValue(Number)
    vUnit = ArgValue(Unit)
    vHf = ArgValue(HundredsForm)
    vPre = ArgValue(Prefix)
    vSuf = ArgValue(Suffix)
    vDec = ArgValue(Decimals)
    vLang = ArgValue(Language)
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
    If IsCellError(vLang) Then
        TAFQIT = vLang
        Exit Function
    End If

    ' Same order of checks as tafqit() in src/index.ts.
    lang = ResolveLanguage(ArgText(vLang))
    If lang < 0 Then
        TAFQIT = mErrMsg(E_INVALID_OPTION)
        Exit Function
    End If
    ui = FindUnit(ArgText(vUnit))
    If ui = 0 Then
        TAFQIT = mErrMsg(E_UNKNOWN_UNIT)
        Exit Function
    End If
    ' Validated for every language, but only Arabic uses it.
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

    words = Phrase(ui, ip, fp, hf, roundIt, lang)
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

' Index of a language id (trimmed, any case; empty = Arabic), or -1 when unknown.
Private Function ResolveLanguage(ByVal s As String) As Long
    Dim t As String, i As Long
    ResolveLanguage = -1
    t = LowerKey(JsTrim(s))
    If t = "" Then
        ResolveLanguage = LANG_AR
        Exit Function
    End If
    For i = 0 To LANG_COUNT - 1
        If t = mLangId(i) Then
            ResolveLanguage = i
            Exit Function
        End If
    Next i
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
' Shared pipeline (src/engine.ts, SPEC sections 8-10 and 17)
'==============================================================================

' Full phrase without prefix/suffix. Sets mErr on error.
Private Function Phrase(ByVal ui As Long, ByVal intPart As String, ByVal fracPart As String, _
        ByVal hf As Long, ByVal roundIt As Boolean, ByVal lang As Long) As String
    Dim subPart As String, subDigits As String, subText As String, d As Long

    mErr = 0
    Phrase = ""
    If Not mUHasForms(ui) Then
        If Len(fracPart) > MAX_FRACTION_DIGITS_NONE Then
            mErr = E_TOO_MANY_DECIMALS
        Else
            Phrase = PlainWords(lang, intPart, fracPart, hf)
        End If
        Exit Function
    End If
    If Not mFHas(ui, lang) Then
        mErr = E_NO_LANGUAGE_FORMS
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
        Phrase = CountWords(lang, intPart, ui, False, hf)
        Exit Function
    End If
    If Not mSHas(ui, lang) Then
        mErr = E_NO_LANGUAGE_FORMS
        Exit Function
    End If
    subText = CountWords(lang, subPart, ui, True, hf)
    If intPart = "0" Then
        Phrase = subText
    Else
        Phrase = CountWords(lang, intPart, ui, False, hf) & mJoiner(lang) & subText
    End If
End Function

' A number with no counted noun (LanguageModule.plain).
Private Function PlainWords(ByVal lang As Long, ByVal intPart As String, ByVal fracPart As String, _
        ByVal hf As Long) As String
    Dim s As String, i As Long
    Select Case lang
    Case LANG_EN
        s = EnInteger(intPart)
        If fracPart <> "" Then
            ' Digits after the point are read one by one.
            s = s & " " & mEnPoint
            For i = 1 To Len(fracPart)
                s = s & " " & mEnSmall(DigitsValue(Mid(fracPart, i, 1)))
            Next i
        End If
    Case LANG_FR
        s = FrInteger(intPart, 0)
        If fracPart <> "" Then s = s & " " & mFrPoint & " " & FractionWords(fracPart, hf, LANG_FR)
    Case Else
        s = IntegerWords(intPart, 0, hf, False)
        If fracPart <> "" Then s = s & " " & mPoint & " " & FractionWords(fracPart, hf, LANG_AR)
    End Select
    PlainWords = s
End Function

' A number followed by the unit's (isSub = False) or subunit's noun (LanguageModule.count).
Private Function CountWords(ByVal lang As Long, ByVal digits As String, ByVal ui As Long, _
        ByVal isSub As Boolean, ByVal hf As Long) As String
    Dim sing As String, plur As String, one As String, dual As String, acc As String, gi As Long
    If isSub Then
        sing = mSSing(ui, lang)
        plur = mSPlur(ui, lang)
        one = mSOne(ui, lang)
        gi = mSGender(ui, lang)
        dual = mSDual(ui)
        acc = mSAcc(ui)
    Else
        sing = mFSing(ui, lang)
        plur = mFPlur(ui, lang)
        one = mFOne(ui, lang)
        gi = mFGender(ui, lang)
        dual = mFDual(ui)
        acc = mFAcc(ui)
    End If
    Select Case lang
    Case LANG_EN
        CountWords = EnCount(digits, sing, plur, one)
    Case LANG_FR
        CountWords = FrCount(digits, sing, plur, one, gi)
    Case Else
        CountWords = CountPhrase(digits, sing, dual, plur, acc, one, gi, hf)
    End Select
End Function

' Split a digit string into 3-digit groups, least significant first; returns the count.
Private Function SplitGroups(ByVal digits As String, ByRef grp() As Long) As Long
    Dim n As Long, k As Long, p As Long
    n = (Len(digits) + 2) \ 3
    ReDim grp(0 To n - 1)
    For k = 0 To n - 1
        p = Len(digits) - 3 * k - 2
        If p < 1 Then
            grp(k) = DigitsValue(Left(digits, Len(digits) - 3 * k))
        Else
            grp(k) = DigitsValue(Mid(digits, p, 3))
        End If
    Next k
    SplitGroups = n
End Function

' Leading zeros of a fraction read as the zero word, then the rest as a whole number
' (Arabic SPEC section 10, French 17.2).
Private Function FractionWords(ByVal fraction As String, ByVal hf As Long, ByVal lang As Long) As String
    Dim lead As Long, i As Long, s As String, zero As String
    If lang = LANG_FR Then
        zero = mFrSmall(0)
    Else
        zero = mZero
    End If
    lead = 0
    Do While lead < Len(fraction)
        If Mid(fraction, lead + 1, 1) <> "0" Then Exit Do
        lead = lead + 1
    Loop
    s = ""
    For i = 1 To lead
        s = JoinParts(s, zero, " ")
    Next i
    If lead < Len(fraction) Then
        If lang = LANG_FR Then
            s = JoinParts(s, FrInteger(Mid(fraction, lead + 1), 0), " ")
        Else
            s = JoinParts(s, IntegerWords(Mid(fraction, lead + 1), 0, hf, False), " ")
        End If
    End If
    FractionWords = s
End Function

'==============================================================================
' Arabic (src/lang/ar.ts, SPEC sections 3-7)
'==============================================================================

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
    Dim n As Long, k As Long, lowest As Long, s As String
    Dim grp() As Long

    If digits = "0" Then
        IntegerWords = mZero
        Exit Function
    End If
    n = SplitGroups(digits, grp)
    lowest = -1
    For k = 0 To n - 1
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

'==============================================================================
' English (src/lang/en.ts, SPEC 17.1)
'==============================================================================

Private Function EnCount(ByVal digits As String, ByVal sing As String, ByVal plur As String, _
        ByVal one As String) As String
    If digits = "1" Then
        If one <> "" Then
            EnCount = one
        Else
            EnCount = mEnSmall(1) & " " & sing
        End If
    Else
        EnCount = EnInteger(digits) & " " & plur
    End If
End Function

Private Function EnInteger(ByVal digits As String) As String
    Dim n As Long, k As Long, s As String
    Dim grp() As Long
    If digits = "0" Then
        EnInteger = mEnSmall(0)
        Exit Function
    End If
    n = SplitGroups(digits, grp)
    s = ""
    For k = n - 1 To 0 Step -1
        If grp(k) > 0 Then
            If k = 0 Then
                s = JoinParts(s, EnGroup(grp(k)), " ")
            Else
                s = JoinParts(s, EnGroup(grp(k)) & " " & mEnScale(k), " ")
            End If
        End If
    Next k
    EnInteger = s
End Function

' 1..999: "Two Hundred Five", "Twenty-One".
Private Function EnGroup(ByVal g As Long) As String
    Dim h As Long, r As Long, s As String, t As String
    h = g \ 100
    r = g Mod 100
    s = ""
    If h > 0 Then s = mEnSmall(h) & " " & mEnHundred
    If r > 0 And r < 20 Then
        s = JoinParts(s, mEnSmall(r), " ")
    ElseIf r >= 20 Then
        t = mEnTens(r \ 10)
        If r Mod 10 <> 0 Then t = t & mEnHyphen & mEnSmall(r Mod 10)
        s = JoinParts(s, t, " ")
    End If
    EnGroup = s
End Function

'==============================================================================
' French (src/lang/fr.ts, SPEC 17.2). Gender index: 0 = m, 1 = f.
'==============================================================================

Private Function FrCount(ByVal digits As String, ByVal sing As String, ByVal plur As String, _
        ByVal one As String, ByVal gi As Long) As String
    Dim link As String
    If digits = "0" Then
        FrCount = mFrSmall(0) & " " & sing
    ElseIf digits = "1" Then
        If one <> "" Then
            FrCount = one
        Else
            FrCount = FrOne(gi) & " " & sing
        End If
    Else
        ' Exactly N million/milliard/billion: un million de dirhams, deux milliards d'euros.
        link = ""
        If Right(digits, 6) = "000000" Then
            link = mFrDe
            If plur <> "" Then
                If FrElides(CharCode(Left(plur, 1))) Then link = mFrDeElided
            End If
        End If
        FrCount = FrInteger(digits, gi) & " " & link & plur
    End If
End Function

Private Function FrOne(ByVal gi As Long) As String
    If gi = 1 Then
        FrOne = mFrOneF
    Else
        FrOne = mFrSmall(1)
    End If
End Function

' Integer words; the gender applies to the last group only, higher groups are masculine.
Private Function FrInteger(ByVal digits As String, ByVal gi As Long) As String
    Dim n As Long, k As Long, g As Long, s As String, part As String
    Dim grp() As Long
    If digits = "0" Then
        FrInteger = mFrSmall(0)
        Exit Function
    End If
    n = SplitGroups(digits, grp)
    s = ""
    For k = n - 1 To 1 Step -1
        g = grp(k)
        If g > 0 Then
            If k = 1 Then
                ' mille: invariable, never "un mille"
                If g = 1 Then
                    part = mFrMille
                Else
                    part = FrGroup(g, 0, False) & " " & mFrMille
                End If
            ElseIf g = 1 Then
                part = FrOne(0) & " " & mFrScale(k)
            Else
                part = FrGroup(g, 0, True) & " " & mFrScale(k) & mFrPluralS
            End If
            s = JoinParts(s, part, " ")
        End If
    Next k
    If grp(0) > 0 Then s = JoinParts(s, FrGroup(grp(0), gi, True), " ")
    FrInteger = s
End Function

' 1..999. cent takes s when multiplied and final (nothing after it in the group).
Private Function FrGroup(ByVal g As Long, ByVal gi As Long, ByVal isFinal As Boolean) As String
    Dim h As Long, r As Long, s As String
    h = g \ 100
    r = g Mod 100
    s = ""
    If h = 1 Then
        s = mFrCent
    ElseIf h > 1 Then
        s = mFrSmall(h) & " " & mFrCent
        If r = 0 And isFinal Then s = s & mFrPluralS
    End If
    If r > 0 Then s = JoinParts(s, FrBelow100(r, gi, isFinal), " ")
    FrGroup = s
End Function

' 1..99. isFinal: no other number word follows, then 80 is written quatre-vingts.
Private Function FrBelow100(ByVal n As Long, ByVal gi As Long, ByVal isFinal As Boolean) As String
    Dim t As Long, un As Long, base As String, s As String
    If n = 1 Then
        s = FrOne(gi)
    ElseIf n < 20 Then
        s = mFrSmall(n)
    Else
        t = n \ 10
        un = n Mod 10
        If t = 7 Or t = 9 Then
            If t = 7 Then
                base = mFrTens(6)
            Else
                base = mFrFourTwenty
            End If
            If t = 7 And un = 1 Then
                s = base & mFrEt & mFrSmall(11)
            Else
                s = base & mFrHyphen & mFrSmall(10 + un)
            End If
        ElseIf t = 8 Then
            If un = 0 Then
                s = mFrFourTwenty
                If isFinal Then s = s & mFrPluralS
            ElseIf un = 1 Then
                s = mFrFourTwenty & mFrHyphen & FrOne(gi)
            Else
                s = mFrFourTwenty & mFrHyphen & mFrSmall(un)
            End If
        ElseIf un = 0 Then
            s = mFrTens(t)
        ElseIf un = 1 Then
            s = mFrTens(t) & mFrEt & FrOne(gi)
        Else
            s = mFrTens(t) & mFrHyphen & mFrSmall(un)
        End If
    End If
    FrBelow100 = s
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
    InitWordsEn
    InitWordsFr
    InitCatalog
    mReady = True
End Sub

'@@GENERATED@@
