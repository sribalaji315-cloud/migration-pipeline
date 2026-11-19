using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Haworth.MdPipeline.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrationToolOnlyImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SwingCarriedFromVersion",
                table: "BronzeVersions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwingCarriedFromVersion",
                table: "BronzeVersions");
        }
    }
}
