using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AvanceObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvanceObraCotas",
                table: "Configuracion",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActividadesObra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Grupo = table.Column<string>(type: "text", nullable: false),
                    Ambito = table.Column<int>(type: "integer", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    Unidad = table.Column<string>(type: "text", nullable: true),
                    ItemContrato = table.Column<string>(type: "text", nullable: true),
                    Efecto = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesObra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InformesDiarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClimaManana = table.Column<string>(type: "text", nullable: true),
                    ClimaTarde = table.Column<string>(type: "text", nullable: true),
                    HorasLluvia = table.Column<decimal>(type: "numeric", nullable: true),
                    ElaboradoPor = table.Column<string>(type: "text", nullable: true),
                    RevisadoPor = table.Column<string>(type: "text", nullable: true),
                    AprobadoPor = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformesDiarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FotosAvance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InformeDiarioId = table.Column<int>(type: "integer", nullable: false),
                    RegistroAvanceId = table.Column<int>(type: "integer", nullable: true),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    Ruta = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosAvance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosAvance_InformesDiarios_InformeDiarioId",
                        column: x => x.InformeDiarioId,
                        principalTable: "InformesDiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotasInformeArea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InformeDiarioId = table.Column<int>(type: "integer", nullable: false),
                    Area = table.Column<string>(type: "text", nullable: false),
                    Realizadas = table.Column<string>(type: "text", nullable: true),
                    Programadas = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasInformeArea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasInformeArea_InformesDiarios_InformeDiarioId",
                        column: x => x.InformeDiarioId,
                        principalTable: "InformesDiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosAvance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InformeDiarioId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ActividadObraId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    AvanceAcumulado = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    NoAplica = table.Column<bool>(type: "boolean", nullable: false),
                    Programado = table.Column<bool>(type: "boolean", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAvance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosAvance_ActividadesObra_ActividadObraId",
                        column: x => x.ActividadObraId,
                        principalTable: "ActividadesObra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosAvance_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosAvance_InformesDiarios_InformeDiarioId",
                        column: x => x.InformeDiarioId,
                        principalTable: "InformesDiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosAvance_InformeDiarioId",
                table: "FotosAvance",
                column: "InformeDiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_InformesDiarios_Fecha",
                table: "InformesDiarios",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_NotasInformeArea_InformeDiarioId",
                table: "NotasInformeArea",
                column: "InformeDiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvance_ActividadObraId",
                table: "RegistrosAvance",
                column: "ActividadObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvance_ElementoTrazadoId_Fecha",
                table: "RegistrosAvance",
                columns: new[] { "ElementoTrazadoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvance_InformeDiarioId",
                table: "RegistrosAvance",
                column: "InformeDiarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosAvance");

            migrationBuilder.DropTable(
                name: "NotasInformeArea");

            migrationBuilder.DropTable(
                name: "RegistrosAvance");

            migrationBuilder.DropTable(
                name: "ActividadesObra");

            migrationBuilder.DropTable(
                name: "InformesDiarios");

            migrationBuilder.DropColumn(
                name: "AvanceObraCotas",
                table: "Configuracion");
        }
    }
}
