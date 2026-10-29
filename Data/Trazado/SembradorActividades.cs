using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Si el proyecto todavía no tiene ninguna Actividad configurada, crea las
// 7 actividades típicas de una línea de transmisión con algunos
// parámetros de ejemplo (a partir de una plantilla de control real).
// Es solo un punto de partida: todo se puede editar, renombrar, agregar
// o desactivar después desde el módulo Actividades — nada de esto queda fijo.
public static class SembradorActividades
{
    public static void SembrarSiVacio(AppDbContext db)
    {
        if (db.Actividades.Any()) return;

        void Crear(string nombreActividad, int orden, params (string codigo, string nombre)[] parametros)
        {
            var actividad = new Actividad { Nombre = nombreActividad, Orden = orden };
            db.Actividades.Add(actividad);
            db.SaveChanges();

            int ordenParam = 0;
            foreach (var (codigo, nombre) in parametros)
            {
                db.ParametrosActividad.Add(new ParametroActividad
                {
                    ActividadId = actividad.Id,
                    Codigo = codigo,
                    Nombre = nombre,
                    Orden = ordenParam++
                });
            }
            db.SaveChanges();
        }

        Crear("Replanteo", 1,
            ("008", "Actas de vecindad"),
            ("012", "Informe de replanteo"),
            ("010", "Planilla de excavación sitio torre"),
            ("003", "Resistividad del terreno"));

        Crear("Obra Civil", 2,
            ("013", "Control de excavaciones"),
            ("014", "Control de rellenos"),
            ("011", "Planilla de nivelación"),
            ("048", "Instalación de acero"),
            ("049", "Control resistencia de cilindros"));

        Crear("Sistema Puesta a Tierra (SPT)", 3,
            ("001", "Instalación SPT"),
            ("002", "Medida de resistencia SPT"));

        Crear("Montaje", 4,
            ("059", "Espesor de pintura"),
            ("051", "Armado de faltantes de montaje"),
            ("052", "Verticalidad y torsión"),
            ("053", "Recepción de torre"));

        Crear("Tendido", 5,
            ("009", "Tendido de cable conductor"),
            ("006", "Pruebas FO"),
            ("005", "Tendido FO"),
            ("007", "Regulación de conductor y guarda"),
            ("005B", "Amarre y grapado"));

        Crear("Pruebas Conductor", 6,
            ("007", "Voltaje y tangente de pérdidas inductiva"),
            ("007B", "Campos electromagnéticos"),
            ("007C", "Secuencia de fases"),
            ("007D", "Resistencia DC"));

        Crear("Pruebas Fibra Óptica", 7,
            ("005", "Atenuación en FO"),
            ("007", "Potencia en FO"),
            ("005B", "Continuidad en FO"),
            ("005C", "Reflectancia en FO"));
    }
}