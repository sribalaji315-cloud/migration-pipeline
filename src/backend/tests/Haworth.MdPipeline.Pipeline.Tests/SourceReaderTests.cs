using ClosedXML.Excel;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Haworth.MdPipeline.Pipeline.Tests;

public sealed class SourceReaderTests
{
    [Fact]
    public void SwingReaderRejectsNonSelectSqlBeforeConnecting()
    {
        var reader = new SwingOdbcReader(MsOptions.Create(new SwingOptions
        {
            OdbcConnectionString = "DSN=does-not-exist",
            Library = "SWLIB",
            AllowedLibraries = ["SWLIB"],
            BomSql = "DELETE FROM {library}.GSST00"
        }));

        Assert.Throws<DomainValidationException>(() => reader.ReadBom(CancellationToken.None));
    }

    [Fact]
    public void SwingReaderRejectsLibraryOutsideAllowlist()
    {
        var reader = new SwingOdbcReader(MsOptions.Create(new SwingOptions { Library = "OTHER", AllowedLibraries = ["SWLIB"] }));

        Assert.Throws<DomainValidationException>(() => reader.ReadItems(CancellationToken.None));
    }

    [Fact]
    public async Task BluestarReaderKeepsSpacePaddedValuesForSilverTrim()
    {
        var spec = BluestarContract.For(BluestarFileKind.ValueListValue);
        using var stream = Workbook(["valuelistid", "VALUE", "DESCRIPTION", "SORTING", "EXTRA"], ["Global_Color", "   F3468002", "Fabric", 1, "x"]);

        var extract = new BluestarExcelReader().Read(spec, stream);
        var rows = new List<string?[]>();
        await foreach (var row in extract.Rows)
        {
            rows.Add(row);
        }

        Assert.Equal<string?>(["Global_Color", "   F3468002", "Fabric", null, "1"], Assert.Single(rows).AsEnumerable());
    }

    [Fact]
    public async Task BluestarReaderAcceptsActualClassListExportHeaders()
    {
        var spec = BluestarContract.For(BluestarFileKind.ClassList);
        using var stream = Workbook(
            ["CLASSNAME", "AXITEMTEMPLATE", "CLASSDESCRIPTION", "CLASSNAMEMASK", "DIRTY", "ENABLEDFORCLASSIFICATION", "EXTENSCLASSNAME",
             "KEYWORDS", "NUMSEQUENCE", "PARTNUMBERPATTERN", "ROOTINALTTREE", "SECONDARYROOT", "STDPART", "SUBTYPE"],
            ["120DegreeDesk", "", "120DegreeDesk", "", "No", "Yes", "Desks", "120DegreeDesk", "ItemFG_A", "", "No", "", "No", "normalClass"]);

        var extract = new BluestarExcelReader().Read(spec, stream);
        var row = Assert.Single(await ToListAsync(extract));

        Assert.Equal("120DegreeDesk", row[extract.Columns.ToList().IndexOf("CLASSDESCRIPTION")]);
        Assert.Equal("ItemFG_A", row[extract.Columns.ToList().IndexOf("NUMSEQUENCE")]);
    }

    [Fact]
    public async Task BluestarReaderStoresAbsentOptionalColumnsAsNull()
    {
        var spec = BluestarContract.For(BluestarFileKind.GlobalVariable);
        using var stream = Workbook(["VARIABLETYPE", "VALUELISTID", "VARIABLEDATATYPE", "VARIABLEVALUES"], ["AccentPlate_Type", "AccentPlate_Type", "StringType", "VALUE_LIST"]);

        var extract = new BluestarExcelReader().Read(spec, stream);
        var row = Assert.Single(await ToListAsync(extract));

        Assert.Equal(spec.Columns, extract.Columns);
        Assert.Null(row[extract.Columns.ToList().IndexOf("UNIT")]);
        Assert.Equal("StringType", row[extract.Columns.ToList().IndexOf("VARIABLEDATATYPE")]);
    }

    private static async Task<List<string?[]>> ToListAsync(SourceExtract extract)
    {
        var rows = new List<string?[]>();
        await foreach (var row in extract.Rows)
        {
            rows.Add(row);
        }

        return rows;
    }

    [Fact]
    public void BluestarReaderReportsMissingColumns()
    {
        var spec = BluestarContract.For(BluestarFileKind.ValueList);
        using var stream = Workbook(["VALUELISTID", "DESCRIPTION"], ["Global_Color", "Colors"]);

        var ex = Assert.Throws<DomainValidationException>(() => new BluestarExcelReader().Read(spec, stream));
        Assert.Contains("VALUETYPE", ex.Message);
    }

    private static MemoryStream Workbook(string[] headers, object[] values)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Sheet1");
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, i + 1).Value = XLCellValue.FromObject(values[i]);
        }

        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
