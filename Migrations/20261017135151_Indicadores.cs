using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class Indicadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Indicadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Formula = table.Column<string>(type: "text", nullable: false),
                    Objetivo = table.Column<string>(type: "text", nullable: false),
                    Meta = table.Column<decimal>(type: "numeric", nullable: true),
                    OperadorMeta = table.Column<string>(type: "text", nullable: false),
                    Unidad = table.Column<string>(type: "text", nullable: false),
                    Frecuencia = table.Column<int>(type: "integer", nullable: false),
                    FrecuenciaTexto = table.Column<string>(type: "text", nullable: true),
                    Tendencia = table.Column<string>(type: "text", nullable: false),
                    TipoIndicador = table.Column<string>(type: "text", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    FuenteInformacion = table.Column<string>(type: "text", nullable: false),
                    Fuente = table.Column<int>(type: "integer", nullable: false),
                    Configuracion = table.Column<string>(type: "text", nullable: true),
                    Acumulado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indicadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicionesIndicador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IndicadorId = table.Column<int>(type: "integer", nullable: false),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: true),
                    NoAplica = table.Column<bool>(type: "boolean", nullable: false),
                    Numerador = table.Column<decimal>(type: "numeric", nullable: true),
                    Denominador = table.Column<decimal>(type: "numeric", nullable: true),
                    Detalle = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Congelado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCongelado = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicionesIndicador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicionesIndicador_Indicadores_IndicadorId",
                        column: x => x.IndicadorId,
                        principalTable: "Indicadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosMedicionIndicador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicionIndicadorId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosMedicionIndicador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosMedicionIndicador_MedicionesIndicador_MedicionIndic~",
                        column: x => x.MedicionIndicadorId,
                        principalTable: "MedicionesIndicador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosMedicionIndicador_MedicionIndicadorId",
                table: "ArchivosMedicionIndicador",
                column: "MedicionIndicadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicionesIndicador_IndicadorId_Anio_Mes",
                table: "MedicionesIndicador",
                columns: new[] { "IndicadorId", "Anio", "Mes" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosMedicionIndicador");

            migrationBuilder.DropTable(
                name: "MedicionesIndicador");

            migrationBuilder.DropTable(
                name: "Indicadores");
        }
    }
}
