// [ETSV] Dominio puro — resultado uniforme de operaciones.
// Espejo de los objetos {ok:true,…} | {ok:false, reason,…} de prototype/js/systems/economy.js
// y pieces.js: la integración (host/server) decide qué hacer con el resultado;
// el dominio nunca lanza excepciones para fallos de regla esperados.

using System.Collections.Generic;

namespace Etsv.Domain
{
    /// <summary>Ganancia de material de un procesamiento (unidades fraccionarias).</summary>
    public class MaterialGain
    {
        public string Mat;
        public double Amount;
    }

    /// <summary>Rendimiento calculado de una receta (materiales o subensambles).</summary>
    public class YieldEntry
    {
        public string Mat;    // material MAT-xx (mutuamente excluyente con Part)
        public string Part;   // pieza genérica PART_DEF
        public double Amount; // unidades (fraccionarias para materiales, enteras para piezas)
    }

    /// <summary>
    /// Resultado de una operación atómica. Un solo tipo (como los objetos JS):
    /// los campos que no aplican quedan en su valor por defecto.
    /// </summary>
    public class OpResult
    {
        public bool Ok;
        public string Reason;          // clave estable de fallo (p. ej. "zona_llena")

        public StoredPart Piece;       // pieza involucrada/creada
        public int Value;              // venta de pieza (redondeado)
        public int Delta;              // venta de material (puede ser negativo: MAT-08)
        public int Restante;           // unidades restantes tras consumir
        public int Cost;               // coste de gestión (residuos especiales)
        public double Units;           // unidades de MAT-08 gestionadas
        public double SpeedMul;        // multiplicador de velocidad por clase de peso
        public int Applied;            // daño de calidad efectivamente aplicado
        public int Quality;            // calidad resultante tras daño
        public double Added;           // rendimiento fraccionario agregado al acumulador
        public double Overflow;        // excedente no agregado por capacidad de contenedor
        public string Zone;            // zona de almacenamiento afectada
        public List<MaterialGain> Gained;    // materiales acreditados por procesamiento
        public List<StoredPart> NewParts;    // subensambles producidos

        public static OpResult Fail(string reason)
        {
            return new OpResult { Ok = false, Reason = reason };
        }
        public static OpResult Success()
        {
            return new OpResult { Ok = true };
        }
    }
}
