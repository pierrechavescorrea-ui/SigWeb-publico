using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class PermitirInformesEnBlanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Informes_TipoEnsayoId_Numero",
                table: "Informes");

            migrationBuilder.CreateIndex(
                name: "IX_Informes_TipoEnsayoId_Numero",
                table: "Informes",
                columns: new[] { "TipoEnsayoId", "Numero" },
                unique: true,
                filter: "\"Numero\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Informes_TipoEnsayoId_Numero",
                table: "Informes");

            migrationBuilder.CreateIndex(
                name: "IX_Informes_TipoEnsayoId_Numero",
                table: "Informes",
                columns: new[] { "TipoEnsayoId", "Numero" },
                unique: true);
        }
    }
}
