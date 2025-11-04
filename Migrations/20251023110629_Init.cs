using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarConnect.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecommendedSystemSize",
                table: "Requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedSystemSize",
                table: "Requests",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
