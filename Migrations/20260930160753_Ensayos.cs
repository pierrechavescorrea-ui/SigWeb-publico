using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class Ensayos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamposEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Etiqueta = table.Column<string>(type: "text", nullable: false),
                    Unidad = table.Column<string>(type: "text", nullable: true),
                    TipoDato = table.Column<int>(type: "integer", nullable: false),
                    Formula = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CamposEnsayo_TiposEnsayo_TipoEnsayoId",
                        column: x => x.TipoEnsayoId,
                        principalTable: "TiposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Informes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Informes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Informes_TiposEnsayo_TipoEnsayoId",
                        column: x => x.TipoEnsayoId,
                        principalTable: "TiposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapeosColumnaEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    EncabezadoTexto = table.Column<string>(type: "text", nullable: false),
                    Destino = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapeosColumnaEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapeosColumnaEnsayo_TiposEnsayo_TipoEnsayoId",
                        column: x => x.TipoEnsayoId,
                        principalTable: "TiposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosInforme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InformeId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosInforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosInforme_Informes_InformeId",
                        column: x => x.InformeId,
                        principalTable: "Informes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ElementoTexto = table.Column<string>(type: "text", nullable: false),
                    SubElemento = table.Column<int>(type: "integer", nullable: false),
                    InformeId = table.Column<int>(type: "integer", nullable: true),
                    DisenoMezclaCodigo = table.Column<string>(type: "text", nullable: true),
                    DisenoMezclaArchivo = table.Column<string>(type: "text", nullable: true),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosEnsayo_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResultadosEnsayo_Informes_InformeId",
                        column: x => x.InformeId,
                        principalTable: "Informes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResultadosEnsayo_TiposEnsayo_TipoEnsayoId",
                        column: x => x.TipoEnsayoId,
                        principalTable: "TiposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosEnsayoComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResultadoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosEnsayoComponente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosEnsayoComponente_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultadosEnsayoComponente_ResultadosEnsayo_ResultadoEnsayo~",
                        column: x => x.ResultadoEnsayoId,
                        principalTable: "ResultadosEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValoresCampoEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResultadoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    CampoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    ValorExcelOriginal = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresCampoEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresCampoEnsayo_CamposEnsayo_CampoEnsayoId",
                        column: x => x.CampoEnsayoId,
                        principalTable: "CamposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValoresCampoEnsayo_ResultadosEnsayo_ResultadoEnsayoId",
                        column: x => x.ResultadoEnsayoId,
                        principalTable: "ResultadosEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosInforme_InformeId",
                table: "ArchivosInforme",
                column: "InformeId");

            migrationBuilder.CreateIndex(
                name: "IX_CamposEnsayo_TipoEnsayoId_Clave",
                table: "CamposEnsayo",
                columns: new[] { "TipoEnsayoId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Informes_TipoEnsayoId_Numero",
                table: "Informes",
                columns: new[] { "TipoEnsayoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapeosColumnaEnsayo_TipoEnsayoId_EncabezadoTexto",
                table: "MapeosColumnaEnsayo",
                columns: new[] { "TipoEnsayoId", "EncabezadoTexto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayo_ElementoTrazadoId",
                table: "ResultadosEnsayo",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayo_InformeId",
                table: "ResultadosEnsayo",
                column: "InformeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayo_TipoEnsayoId",
                table: "ResultadosEnsayo",
                column: "TipoEnsayoId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayoComponente_ComponenteId",
                table: "ResultadosEnsayoComponente",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayoComponente_ResultadoEnsayoId_ComponenteId",
                table: "ResultadosEnsayoComponente",
                columns: new[] { "ResultadoEnsayoId", "ComponenteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoEnsayo_CampoEnsayoId",
                table: "ValoresCampoEnsayo",
                column: "CampoEnsayoId");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoEnsayo_ResultadoEnsayoId_CampoEnsayoId",
                table: "ValoresCampoEnsayo",
                columns: new[] { "ResultadoEnsayoId", "CampoEnsayoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosInforme");

            migrationBuilder.DropTable(
                name: "MapeosColumnaEnsayo");

            migrationBuilder.DropTable(
                name: "ResultadosEnsayoComponente");

            migrationBuilder.DropTable(
                name: "ValoresCampoEnsayo");

            migrationBuilder.DropTable(
                name: "CamposEnsayo");

            migrationBuilder.DropTable(
                name: "ResultadosEnsayo");

            migrationBuilder.DropTable(
                name: "Informes");
        }
    }
}
