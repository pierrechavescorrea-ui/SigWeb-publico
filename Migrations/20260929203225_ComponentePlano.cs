using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ComponentePlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"ComponentesPlano\";");

            migrationBuilder.DropForeignKey(
                name: "FK_ComponentesPlano_PlanosMaestro_PlanoMaestroId",
                table: "ComponentesPlano");

            migrationBuilder.RenameColumn(
                name: "PlanoMaestroId",
                table: "ComponentesPlano",
                newName: "ArchivoPlanoMaestroId");

            migrationBuilder.RenameIndex(
                name: "IX_ComponentesPlano_PlanoMaestroId",
                table: "ComponentesPlano",
                newName: "IX_ComponentesPlano_ArchivoPlanoMaestroId");

            migrationBuilder.RenameIndex(
                name: "IX_ComponentesPlano_ComponenteId_PlanoMaestroId",
                table: "ComponentesPlano",
                newName: "IX_ComponentesPlano_ComponenteId_ArchivoPlanoMaestroId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComponentesPlano_ArchivosPlanoMaestro_ArchivoPlanoMaestroId",
                table: "ComponentesPlano",
                column: "ArchivoPlanoMaestroId",
                principalTable: "ArchivosPlanoMaestro",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComponentesPlano_ArchivosPlanoMaestro_ArchivoPlanoMaestroId",
                table: "ComponentesPlano");

            migrationBuilder.RenameColumn(
                name: "ArchivoPlanoMaestroId",
                table: "ComponentesPlano",
                newName: "PlanoMaestroId");

            migrationBuilder.RenameIndex(
                name: "IX_ComponentesPlano_ComponenteId_ArchivoPlanoMaestroId",
                table: "ComponentesPlano",
                newName: "IX_ComponentesPlano_ComponenteId_PlanoMaestroId");

            migrationBuilder.RenameIndex(
                name: "IX_ComponentesPlano_ArchivoPlanoMaestroId",
                table: "ComponentesPlano",
                newName: "IX_ComponentesPlano_PlanoMaestroId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComponentesPlano_PlanosMaestro_PlanoMaestroId",
                table: "ComponentesPlano",
                column: "PlanoMaestroId",
                principalTable: "PlanosMaestro",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}