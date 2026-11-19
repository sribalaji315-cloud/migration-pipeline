using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Haworth.MdPipeline.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwingOnlyImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SyncScope",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "All");

            migrationBuilder.AddColumn<int>(
                name: "MtCarriedFromVersion",
                table: "BronzeVersions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncScope",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "MtCarriedFromVersion",
                table: "BronzeVersions");
        }
    }
}
