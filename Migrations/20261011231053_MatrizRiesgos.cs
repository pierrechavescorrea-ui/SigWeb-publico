using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class MatrizRiesgos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VersionesMatrizRiesgos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    DescripcionCambios = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    VersionAnteriorId = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesMatrizRiesgos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesMatrizRiesgos_VersionesMatrizRiesgos_VersionAnteri~",
                        column: x => x.VersionAnteriorId,
                        principalTable: "VersionesMatrizRiesgos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Riesgos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionMatrizRiesgosId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    EstadoActual = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Area = table.Column<string>(type: "text", nullable: false),
                    Causa = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Consecuencia = table.Column<string>(type: "text", nullable: false),
                    Probabilidad = table.Column<int>(type: "integer", nullable: false),
                    Impacto = table.Column<int>(type: "integer", nullable: false),
                    Disparadores = table.Column<string>(type: "text", nullable: false),
                    Estrategia = table.Column<string>(type: "text", nullable: false),
                    PlanRespuesta = table.Column<string>(type: "text", nullable: false),
                    ResponsablePlan = table.Column<string>(type: "text", nullable: false),
                    EstadoPlan = table.Column<string>(type: "text", nullable: false),
                    ComoSeHaceSeguimiento = table.Column<string>(type: "text", nullable: false),
                    RiesgoAnteriorId = table.Column<int>(type: "integer", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riesgos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Riesgos_Riesgos_RiesgoAnteriorId",
                        column: x => x.RiesgoAnteriorId,
                        principalTable: "Riesgos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Riesgos_VersionesMatrizRiesgos_VersionMatrizRiesgosId",
                        column: x => x.VersionMatrizRiesgosId,
                        principalTable: "VersionesMatrizRiesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlcancesRiesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RiesgoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlcancesRiesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlcancesRiesgo_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlcancesRiesgo_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlcancesRiesgo_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlcancesRiesgo_Riesgos_RiesgoId",
                        column: x => x.RiesgoId,
                        principalTable: "Riesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlcancesRiesgo_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeguimientosRiesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RiesgoId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    Resultado = table.Column<int>(type: "integer", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: false),
                    Probabilidad = table.Column<int>(type: "integer", nullable: true),
                    Impacto = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeguimientosRiesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeguimientosRiesgo_Riesgos_RiesgoId",
                        column: x => x.RiesgoId,
                        principalTable: "Riesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallazgosRiesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeguimientoRiesgoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Accion = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CerradoPor = table.Column<string>(type: "text", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallazgosRiesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallazgosRiesgo_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HallazgosRiesgo_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HallazgosRiesgo_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HallazgosRiesgo_SeguimientosRiesgo_SeguimientoRiesgoId",
                        column: x => x.SeguimientoRiesgoId,
                        principalTable: "SeguimientosRiesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallazgosRiesgo_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosRiesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeguimientoRiesgoId = table.Column<int>(type: "integer", nullable: true),
                    HallazgoRiesgoId = table.Column<int>(type: "integer", nullable: true),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosRiesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosRiesgo_HallazgosRiesgo_HallazgoRiesgoId",
                        column: x => x.HallazgoRiesgoId,
                        principalTable: "HallazgosRiesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivosRiesgo_SeguimientosRiesgo_SeguimientoRiesgoId",
                        column: x => x.SeguimientoRiesgoId,
                        principalTable: "SeguimientosRiesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlcancesRiesgo_ComponenteId",
                table: "AlcancesRiesgo",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_AlcancesRiesgo_ElementoTrazadoId",
                table: "AlcancesRiesgo",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlcancesRiesgo_ItemCatalogoId",
                table: "AlcancesRiesgo",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlcancesRiesgo_RiesgoId",
                table: "AlcancesRiesgo",
                column: "RiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlcancesRiesgo_TiroId",
                table: "AlcancesRiesgo",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRiesgo_HallazgoRiesgoId",
                table: "ArchivosRiesgo",
                column: "HallazgoRiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRiesgo_SeguimientoRiesgoId",
                table: "ArchivosRiesgo",
                column: "SeguimientoRiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosRiesgo_ComponenteId",
                table: "HallazgosRiesgo",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosRiesgo_ElementoTrazadoId",
                table: "HallazgosRiesgo",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosRiesgo_ItemCatalogoId",
                table: "HallazgosRiesgo",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosRiesgo_SeguimientoRiesgoId",
                table: "HallazgosRiesgo",
                column: "SeguimientoRiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosRiesgo_TiroId",
                table: "HallazgosRiesgo",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_Riesgos_RiesgoAnteriorId",
                table: "Riesgos",
                column: "RiesgoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_Riesgos_VersionMatrizRiesgosId_Codigo",
                table: "Riesgos",
                columns: new[] { "VersionMatrizRiesgosId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeguimientosRiesgo_RiesgoId_Fecha",
                table: "SeguimientosRiesgo",
                columns: new[] { "RiesgoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_VersionesMatrizRiesgos_Codigo",
                table: "VersionesMatrizRiesgos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionesMatrizRiesgos_VersionAnteriorId",
                table: "VersionesMatrizRiesgos",
                column: "VersionAnteriorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlcancesRiesgo");

            migrationBuilder.DropTable(
                name: "ArchivosRiesgo");

            migrationBuilder.DropTable(
                name: "HallazgosRiesgo");

            migrationBuilder.DropTable(
                name: "SeguimientosRiesgo");

            migrationBuilder.DropTable(
                name: "Riesgos");

            migrationBuilder.DropTable(
                name: "VersionesMatrizRiesgos");
        }
    }
}
