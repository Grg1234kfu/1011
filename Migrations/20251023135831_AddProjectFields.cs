using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProductsUsed",
                table: "Projects",
                newName: "ProgressStage");

            migrationBuilder.RenameColumn(
                name: "CompletedDate",
                table: "Projects",
                newName: "CompletionDate");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "InstallationNotes",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "MaterialsUsed",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialsUsed",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "ProgressStage",
                table: "Projects",
                newName: "ProductsUsed");

            migrationBuilder.RenameColumn(
                name: "CompletionDate",
                table: "Projects",
                newName: "CompletedDate");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "InstallationNotes",
                table: "Projects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);
        }
    }
}
