using SigWeb.Models;

namespace SigWeb.Data.Ensayos;

// Siembra la galería de íconos con un punto de partida: formas para
// ensayos de laboratorio (concretos, suelos), y genéricas de obra civil,
// eléctrico y torres. El usuario puede subir los suyos propios, renombrar
// o desactivar cualquiera desde Catálogos → Galería de íconos. Seguro de
// llamar en cada arranque: si ya existe (por Clave), no lo vuelve a crear.
public static class SembradorIconosCatalogo
{
    public static void SembrarSiHaceFalta(AppDbContext db)
    {
        var yaExisten = db.IconosCatalogo.Select(i => i.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Crear(string clave, string nombre, string ruta, string categoria)
        {
            if (yaExisten.Contains(clave)) return;
            db.IconosCatalogo.Add(new IconoCatalogo
            {
                Clave = clave,
                Nombre = nombre,
                RutaArchivo = ruta,
                Categoria = categoria
            });
        }

        Crear("cilindro-concreto", "Cilindro de concreto", "images/iconos/cilindro-concreto.svg", "Concretos");
        Crear("slump", "Asentamiento (Slump)", "images/iconos/slump.svg", "Concretos");
        Crear("cono-arena", "Cono y arena (densidad in-situ)", "images/iconos/cono-arena.svg", "Suelos");
        Crear("proctor", "Proctor (compactación)", "images/iconos/proctor.svg", "Suelos");
        Crear("dcp", "Penetrómetro dinámico de cono (PDC)", "images/iconos/dcp.svg", "Suelos");
        Crear("suelos", "Suelos (estratos)", "images/iconos/suelos.svg", "Suelos");
        Crear("obra-civil", "Casco (obra civil)", "images/iconos/obra-civil.svg", "Obra civil");
        Crear("viga", "Viga / estructura", "images/iconos/viga.svg", "Obra civil");
        Crear("electrico", "Eléctrico (rayo)", "images/iconos/electrico.svg", "Eléctrico");
        Crear("torre", "Torre de energía", "images/icono-torre.svg", "Torres");
        Crear("pata", "Pata (pedestal + stub)", "images/icono-pata.svg", "Torres");
        Crear("tiro", "Tiro (torres con cable)", "images/icono-tiro.svg", "Torres");
        Crear("planos-estructural", "Planos — Estructural", "images/iconos/planos-estructural.svg", "Planos");
        Crear("planos-civil", "Planos — Civil", "images/iconos/planos-civil.svg", "Planos");
        Crear("planos-electrico", "Planos — Eléctrico", "images/iconos/planos-electrico.svg", "Planos");
        Crear("catalogo", "Catálogo", "images/iconos/catalogo.svg", "General");
        Crear("pdf", "Documento PDF", "images/iconos/pdf.svg", "General");
        Crear("laboratorio", "Laboratorio (matraz)", "images/iconos/laboratorio.svg", "Laboratorio");
        Crear("balanza", "Balanza (peso/densidad)", "images/iconos/balanza.svg", "Laboratorio");
        Crear("regla", "Regla / calibrador", "images/iconos/regla.svg", "Laboratorio");
        Crear("documento", "Documento / informe", "images/iconos/documento.svg", "General");

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
