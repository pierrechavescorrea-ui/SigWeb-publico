using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTrazado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElementoTrazadoId",
                table: "Materiales",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElementosTrazado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: false),
                    Longitud = table.Column<double>(type: "double precision", nullable: false),
                    ElementoPadreId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementosTrazado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementosTrazado_ElementosTrazado_ElementoPadreId",
                        column: x => x.ElementoPadreId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_ElementoTrazadoId",
                table: "Materiales",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosTrazado_ElementoPadreId",
                table: "ElementosTrazado",
                column: "ElementoPadreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materiales_ElementosTrazado_ElementoTrazadoId",
                table: "Materiales",
                column: "ElementoTrazadoId",
                principalTable: "ElementosTrazado",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materiales_ElementosTrazado_ElementoTrazadoId",
                table: "Materiales");

            migrationBuilder.DropTable(
                name: "ElementosTrazado");

            migrationBuilder.DropIndex(
                name: "IX_Materiales_ElementoTrazadoId",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "ElementoTrazadoId",
                table: "Materiales");
        }
    }
}
