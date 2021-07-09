using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace LimitedRegen
{
    /// <summary>
    /// A <c>HediffComp</c> applied to the default missing part. It allows
    /// missing parts to be removed during the <c>HealthTick</c> without
    /// causing errors.
    /// </summary>
    class HediffComp_ShouldRemove : HediffComp
    {
        /// <summary>
        /// Informs developers if a <c>Hediff</c> is supposed to be removed. If
        /// this text is visible on an active <c>Hediff</c>, something is
        /// wrong.
        /// </summary>
        /// <returns>
        /// Updated debug string.
        /// </returns>
        public override string CompDebugString()
        {
            return base.CompDebugString() + 
                "Should be removed next tick.";
        }

        /// <summary>
        /// This method is used to determine if a HediffComp's parent 
        /// Hediff should be removed when it is safe to do so. It is run by
        /// HediffWithComps.ShouldRemove. Some Hediffs,
        /// like Hediff_MissingPart, override this method and must be patched.
        /// </summary>
        public override bool CompShouldRemove => true;
    }
}
