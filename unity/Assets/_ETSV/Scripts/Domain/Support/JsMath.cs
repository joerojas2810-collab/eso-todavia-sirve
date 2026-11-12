// [ETSV] Dominio puro — sin UnityEngine (ETSV.Domain.asmdef: noEngineReferences).
// Port 1:1 de las utilidades numéricas del prototipo JS.

using System;

namespace Etsv.Domain
{
    /// <summary>
    /// Paridad numérica con JavaScript. JS Math.round redondea .5 hacia +∞;
    /// C# Math.Round usa por defecto "banker's rounding" (76.5 → 76). Para que
    /// los valores de venta sean idénticos al prototipo (y entre ambos motores)
    /// se usa Floor(x + 0.5) explícitamente.
    /// </summary>
    public static class JsMath
    {
        public static int Round(double x)
        {
            return (int)Math.Floor(x + 0.5);
        }

        public static int ClampInt(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
