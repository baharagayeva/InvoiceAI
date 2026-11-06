using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    SellerName = table.Column<string>(type: "text", nullable: true),
                    SellerTaxId = table.Column<string>(type: "text", nullable: true),
                    BuyerName = table.Column<string>(type: "text", nullable: true),
                    BuyerTaxId = table.Column<string>(type: "text", nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDateCalculated = table.Column<bool>(type: "boolean", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true),
                    LatePenaltyTerms = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Warnings = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BuyerTaxId",
                table: "Invoices",
                column: "BuyerTaxId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerTaxId",
                table: "Invoices",
                column: "SellerTaxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
