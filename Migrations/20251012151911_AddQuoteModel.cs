using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarConnect.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    SystemSize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InstallationCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PanelBrand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InverterBrand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatteryBrand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SystemDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedInstallationDays = table.Column<int>(type: "int", nullable: false),
                    WarrantyYears = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientResponse = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Quotes_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_RequestId",
                table: "Quotes",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_VendorId",
                table: "Quotes",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
