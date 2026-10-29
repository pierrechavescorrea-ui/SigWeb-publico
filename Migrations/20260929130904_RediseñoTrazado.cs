using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class RediseñoTrazado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElementosTrazado_ElementosTrazado_ElementoPadreId",
                table: "ElementosTrazado");

            migrationBuilder.DropForeignKey(
                name: "FK_Materiales_ElementosTrazado_ElementoTrazadoId",
                table: "Materiales");

            migrationBuilder.DropIndex(
                name: "IX_ElementosTrazado_ElementoPadreId",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "ElementoPadreId",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "ElementosTrazado");

            migrationBuilder.RenameColumn(
                name: "ElementoTrazadoId",
                table: "Materiales",
                newName: "ComponenteId");

            migrationBuilder.RenameIndex(
                name: "IX_Materiales_ElementoTrazadoId",
                table: "Materiales",
                newName: "IX_Materiales_ComponenteId");

            migrationBuilder.CreateTable(
                name: "Actividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actividades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Componentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    EsPredeterminado = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Componentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Componentes_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosActividad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActividadId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosActividad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametrosActividad_Actividades_ActividadId",
                        column: x => x.ActividadId,
                        principalTable: "Actividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosParametro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false),
                    ParametroActividadId = table.Column<int>(type: "integer", nullable: false),
                    Completado = table.Column<bool>(type: "boolean", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    RutaArchivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosParametro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosParametro_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosParametro_ParametrosActividad_ParametroActividadId",
                        column: x => x.ParametroActividadId,
                        principalTable: "ParametrosActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Componentes_ElementoTrazadoId",
                table: "Componentes",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosActividad_ActividadId",
                table: "ParametrosActividad",
                column: "ActividadId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametro_ComponenteId",
                table: "RegistrosParametro",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametro_ParametroActividadId",
                table: "RegistrosParametro",
                column: "ParametroActividadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materiales_Componentes_ComponenteId",
                table: "Materiales",
                column: "ComponenteId",
                principalTable: "Componentes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materiales_Componentes_ComponenteId",
                table: "Materiales");

            migrationBuilder.DropTable(
                name: "RegistrosParametro");

            migrationBuilder.DropTable(
                name: "Componentes");

            migrationBuilder.DropTable(
                name: "ParametrosActividad");

            migrationBuilder.DropTable(
                name: "Actividades");

            migrationBuilder.RenameColumn(
                name: "ComponenteId",
                table: "Materiales",
                newName: "ElementoTrazadoId");

            migrationBuilder.RenameIndex(
                name: "IX_Materiales_ComponenteId",
                table: "Materiales",
                newName: "IX_Materiales_ElementoTrazadoId");

            migrationBuilder.AddColumn<int>(
                name: "ElementoPadreId",
                table: "ElementosTrazado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "ElementosTrazado",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosTrazado_ElementoPadreId",
                table: "ElementosTrazado",
                column: "ElementoPadreId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElementosTrazado_ElementosTrazado_ElementoPadreId",
                table: "ElementosTrazado",
                column: "ElementoPadreId",
                principalTable: "ElementosTrazado",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materiales_ElementosTrazado_ElementoTrazadoId",
                table: "Materiales",
                column: "ElementoTrazadoId",
                principalTable: "ElementosTrazado",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
