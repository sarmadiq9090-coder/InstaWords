namespace InstaWords.Core.Internal
{
    /// <summary>Forms of a counted noun in one language.</summary>
    internal abstract class Forms
    {
    }

    /// <summary>Arabic forms (src/types.ts NounForms).</summary>
    internal sealed class NounForms : Forms
    {
        internal NounForms(string singular, string dual, string plural, string accusative, bool feminine, string? one)
        {
            Singular = singular;
            Dual = dual;
            Plural = plural;
            Accusative = accusative;
            Feminine = feminine;
            One = one;
        }

        internal string Singular;
        internal string Dual;
        internal string Plural;
        internal string Accusative;
        internal bool Feminine;
        internal string? One;
    }

    /// <summary>English or French forms (src/types.ts EnglishForms / FrenchForms). English ignores the gender.</summary>
    internal sealed class ForeignForms : Forms
    {
        internal ForeignForms(string singular, string plural, string? one, bool feminine)
        {
            Singular = singular;
            Plural = plural;
            One = one;
            Feminine = feminine;
        }

        internal string Singular;
        internal string Plural;
        internal string? One;
        internal bool Feminine;
    }

    internal sealed class SubunitData
    {
        internal SubunitData(int digits, NounForms forms, ForeignForms? en, ForeignForms? fr)
        {
            Digits = digits;
            Forms = forms;
            En = en;
            Fr = fr;
        }

        /// <summary>Fraction digits written as the subunit: String(ratio).length - 1.</summary>
        internal int Digits;
        internal NounForms Forms;
        internal ForeignForms? En;
        internal ForeignForms? Fr;
    }

    /// <summary>One unit of data/units.json, with its lookup keys precomputed by the generator.</summary>
    internal sealed class UnitData
    {
        internal UnitData(string id, string category, string label, string? code, string[] aliases,
            NounForms? forms, ForeignForms? en, ForeignForms? fr, SubunitData? subunit, string[] keys)
        {
            Id = id;
            Category = category;
            Label = label;
            Code = code;
            Aliases = aliases;
            Forms = forms;
            En = en;
            Fr = fr;
            Subunit = subunit;
            Keys = keys;
        }

        internal string Id;
        internal string Category;
        internal string Label;
        internal string? Code;
        internal string[] Aliases;
        /// <summary>Null only for "none" (a number without a unit).</summary>
        internal NounForms? Forms;
        internal ForeignForms? En;
        internal ForeignForms? Fr;
        internal SubunitData? Subunit;
        /// <summary>unitKey() of the id, label, code and aliases (id first), as src/catalog.ts indexes them.</summary>
        internal string[] Keys;
    }
}
