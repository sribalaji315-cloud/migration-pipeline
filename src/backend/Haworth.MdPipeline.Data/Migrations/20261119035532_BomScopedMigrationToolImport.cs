using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Haworth.MdPipeline.Data.Migrations
{
    /// <inheritdoc />
    public partial class BomScopedMigrationToolImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeTopBomsJson",
                table: "PipelineRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MtScopeItems",
                table: "BronzeVersions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MtScopeTopBomsJson",
                table: "BronzeVersions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeTopBomsJson",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "MtScopeItems",
                table: "BronzeVersions");

            migrationBuilder.DropColumn(
                name: "MtScopeTopBomsJson",
                table: "BronzeVersions");
        }
    }
}
