// [ETSV] Dominio puro — sin UnityEngine (la asmdef tiene noEngineReferences: true).
// Este archivo es identidad/configuración de diseño, NO una regla de juego:
// las reglas (piezas, almacenes, economía, desmontaje) se portarán acá en la
// próxima instrucción desde prototype/js (73 tests como criterio de paridad).
// Fuente de verdad: los cinco GDD (docs/).

namespace Etsv.Domain
{
    /// <summary>
    /// Constantes de identidad y diseño compartidas por todo el proyecto.
    /// </summary>
    public static class GameInfo
    {
        /// <summary>Título del producto (GDD 1.0).</summary>
        public const string Title = "¡ESO TODAVÍA SIRVE!";

        /// <summary>Versión del esquema de guardado. Debe coincidir con la del
        /// prototipo (prototype/js/state/gameState.js → VERSION = 3) hasta que
        /// el save nativo lo reemplace.</summary>
        public const int SaveVersion = 3;

        /// <summary>Versión del formato COMPLETO de producción (Instrucción 08):
        /// id de partida, patio físico, poses de piezas, contadores y progreso.
        /// La migración 3 → 4 es explícita (SaveMigrations.Migrate3To4).</summary>
        public const int SaveVersionFull = 4;

        /// <summary>Jugadores por sesión: 1–7 (GDD 1.0/2.0, cooperativo).
        /// Fuente única: de acá lee SessionConfig (capa de integración).</summary>
        public const int MaxPlayers = 7;
    }
}
