// [ETSV] Archivo propio de ¡ESO TODAVÍA SIRVE! (no proviene de U6-FPM).
// Ubicación deliberada: fuera de una asmdef → Assembly-CSharp, para que los
// scripts upstream (RelayManager, PlayerSpawnManager, etc.) puedan referenciarlo
// sin modificar la estructura de assemblies de la base (facilita updates).
//
// Parámetros ÚNICOS de sesión. Los scripts de U6-FPM leían estos valores
// hardcodeados (MaxPlayers = 4 en RelayManager, aprobación por cantidad de
// spawn points, Vivox siempre activo). Ahora todos leen de acá.
// Fuente de verdad del diseño: GDD 1.0/2.0 (1–7 jugadores, coop, host autoritativo).

/// <summary>
/// Configuración central de sesión de ¡ESO TODAVÍA SIRVE!.
/// </summary>
public static class SessionConfig
{
    /// <summary>Capacidad de la sesión: 1–7 jugadores (GDD 1.0). Relay soporta el máximo
    /// como conexiones = MaxPlayers - 1 (el host ocupa un lugar).
    /// Fuente única del número: Etsv.Domain.GameInfo (dominio puro).</summary>
    public const int MaxPlayers = Etsv.Domain.GameInfo.MaxPlayers;

    /// <summary>Vivox queda como dependencia disponible (decisión Instrucción 04 #4)
    /// pero su inicialización está DESACTIVADA hasta trabajar comunicación por voz.
    /// Poner en true cuando se integre voz: no hace falta ningún otro cambio.</summary>
    public const bool VoiceEnabled = false;

    /// <summary>Título del producto (fuente única: dominio puro).</summary>
    public const string Title = Etsv.Domain.GameInfo.Title;
}
