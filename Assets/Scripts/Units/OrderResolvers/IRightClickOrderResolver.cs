using UnityEngine;

namespace MechTS.Units
{
    /// <summary>
    /// One resolvable right-click order type in <see cref="AttackCommand"/>'s dispatch chain
    /// (issue #60). Each implementation owns whatever raycast(s)/state it needs internally —
    /// the interface only needs the click's camera ray.
    /// </summary>
    public interface IRightClickOrderResolver
    {
        /// <summary>
        /// Attempts to resolve this right-click into this resolver's order type. Returns true
        /// if it did (consuming the click, so no lower-priority resolver runs), false to let
        /// <see cref="AttackCommand"/> try the next resolver in priority order.
        /// </summary>
        /// <param name="ray">The right-click's camera ray.</param>
        bool TryHandle(Ray ray);
    }
}
