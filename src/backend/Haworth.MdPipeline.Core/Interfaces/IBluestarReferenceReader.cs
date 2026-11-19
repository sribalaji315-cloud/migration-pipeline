namespace Haworth.MdPipeline.Core.Interfaces;

public enum BluestarFileKind
{
    ClassList,
    ClassVariable,
    GlobalVariable,
    ValueList,
    ValueListValue
}

/// <param name="Columns">Every known export column, stored under its source name in Bronze. Absent optional columns become NULL.</param>
/// <param name="RequiredColumns">Columns the Silver steps depend on; an upload without them is rejected.</param>
public sealed record BluestarFileSpec(
    BluestarFileKind Kind,
    string FileName,
    string BronzeTable,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> RequiredColumns);

/// <summary>Column names follow the actual Bluestar export headers (verified against a production export).</summary>
public static class BluestarContract
{
    public static readonly IReadOnlyList<BluestarFileSpec> Files =
    [
        new(BluestarFileKind.ClassList, "Bluestar classification list.xlsx", "bs_class",
            ["CLASSNAME", "AXITEMTEMPLATE", "CLASSDESCRIPTION", "CLASSNAMEMASK", "DIRTY", "ENABLEDFORCLASSIFICATION", "EXTENSCLASSNAME",
             "KEYWORDS", "NUMSEQUENCE", "PARTNUMBERPATTERN", "ROOTINALTTREE", "SECONDARYROOT", "STDPART", "SUBTYPE"],
            ["CLASSNAME", "EXTENSCLASSNAME", "ENABLEDFORCLASSIFICATION"]),
        new(BluestarFileKind.ClassVariable, "Bluestar classification variable name table.xlsx", "bs_class_variable",
            ["CLASSNAME", "DISPLAYNAME", "VARIABLENAME", "ATTRIBUTEID", "DEFAULTVALUE", "DEFAULTVALUEINT", "DEFAULTVALUEREAL",
             "ECADATTRIBUTENAME", "FIXEDDECIMAL", "ISCADPARAMETER", "ISCALCULATABLE", "ISURL", "LIFECYCLE", "MANDATORY", "OBSOLETE",
             "SEQUENCE", "SILICONEXPERTFIELD", "TOLERANCEMINUS", "TOLERANCEPLUS", "UNIT", "VALIDATECHILDDATA", "VALUELISTID",
             "VALUETYPE", "VARIABLEVALUES"],
            ["CLASSNAME", "VARIABLENAME", "VALUELISTID", "VARIABLEVALUES", "VALUETYPE", "MANDATORY", "OBSOLETE"]),
        // VARIABLETYPE holds the global variable name; VARIABLEDATATYPE its value type.
        new(BluestarFileKind.GlobalVariable, "Bluestar global variables.xlsx", "bs_global_variable",
            ["VARIABLETYPE", "ALLOWMANUAL", "EXTERNALID", "UNIT", "VALUELISTID", "VARIABLEDATATYPE", "VARIABLEVALUES"],
            ["VARIABLETYPE", "VALUELISTID", "VARIABLEDATATYPE", "VARIABLEVALUES"]),
        new(BluestarFileKind.ValueList, "BS Value Variable List table.xlsx", "bs_valuelist",
            ["VALUELISTID", "BS_VARIABLELISTFILTER", "DESCRIPTION", "PARENTVALUELISTID", "VALUETYPE"],
            ["VALUELISTID", "VALUETYPE"]),
        new(BluestarFileKind.ValueListValue, "Bluestar GlobalVariableList.xlsx", "bs_valuelist_value",
            ["VALUELISTID", "VALUE", "DESCRIPTION", "PARTNUMBERVALUE", "SORTING"],
            ["VALUELISTID", "VALUE"])
    ];

    public static BluestarFileSpec For(BluestarFileKind kind) => Files.Single(f => f.Kind == kind);
}

public interface IBluestarReferenceReader
{
    /// <summary>Reads the first worksheet; values are returned untrimmed (value list values are space-padded and trimmed in Silver).</summary>
    SourceExtract Read(BluestarFileSpec spec, Stream workbook);
}
