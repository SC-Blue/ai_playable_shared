using System.Collections.Generic;
using System.Linq;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class CatalogGameplayShapeRules
    {
        public const string ShapeStraight = "straight";
        public const string ShapeCorner = "corner";
        public const string ShapeTJunction = "t_junction";
        public const string ShapeCross = "cross";

        private static readonly string[] RailRequiredShapes = { ShapeStraight, ShapeCorner };
        private static readonly string[] Connected3RequiredShapes = { ShapeStraight, ShapeCorner, ShapeCross };
        private static readonly string[] Connected3AllowedShapes = { ShapeStraight, ShapeCorner, ShapeTJunction, ShapeCross };

        public static bool RequiresGameplayFootprint(string role)
        {
            string normalizedRole = Normalize(role);
            return string.Equals(normalizedRole, CatalogGameplayTaxonomy.RoleGenerator, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedRole, CatalogGameplayTaxonomy.RoleProcessor, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedRole, CatalogGameplayTaxonomy.RoleSeller, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedRole, CatalogGameplayTaxonomy.RoleUnlockPad, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedRole, CatalogGameplayTaxonomy.RoleRail, System.StringComparison.Ordinal);
        }

        public static bool RequiresRailPathShapes()
        {
            return true;
        }

        public static bool IsValidRailPathShapeSet(IEnumerable<string> shapes)
        {
            HashSet<string> normalized = NormalizeSet(shapes, RailRequiredShapes);
            return normalized.SetEquals(RailRequiredShapes);
        }

        public static bool IsValidEnvironmentPerimeterShapeSet(string placementMode, string variationMode, IEnumerable<string> shapes)
        {
            HashSet<string> normalized = NormalizeSet(shapes, Connected3AllowedShapes);
            if (!IsConnected3Perimeter(placementMode, variationMode))
                return normalized.Count == 0;

            return normalized.All(value => Connected3AllowedShapes.Contains(value, System.StringComparer.Ordinal)) &&
                   Connected3RequiredShapes.All(normalized.Contains);
        }

        public static bool RequiresEnvironmentFootprint(string role, string placementMode, string variationMode)
        {
            return !string.IsNullOrWhiteSpace(Normalize(role)) &&
                   !string.IsNullOrWhiteSpace(Normalize(placementMode)) &&
                   !string.IsNullOrWhiteSpace(Normalize(variationMode));
        }

        public static bool RequiresConnected3Cross()
        {
            return true;
        }

        public static bool RequiresConnected3TJunctionOnlyIfDeclared()
        {
            return true;
        }

        public static bool IsConnected3Perimeter(string placementMode, string variationMode)
        {
            return string.Equals(Normalize(placementMode), EnvironmentCatalog.PLACEMENT_MODE_PERIMETER, System.StringComparison.Ordinal) &&
                   string.Equals(Normalize(variationMode), EnvironmentCatalog.VARIATION_MODE_CONNECTED3, System.StringComparison.Ordinal);
        }

        public static bool HasShape(IEnumerable<string> shapes, string expectedShape)
        {
            string normalizedExpectedShape = Normalize(expectedShape);
            if (string.IsNullOrWhiteSpace(normalizedExpectedShape))
                return false;

            return NormalizeSet(shapes, Connected3AllowedShapes.Concat(RailRequiredShapes))
                .Contains(normalizedExpectedShape);
        }

        public static string[] NormalizeRailPathShapes(IEnumerable<string> shapes)
        {
            return NormalizeSet(shapes, RailRequiredShapes)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] NormalizeEnvironmentPerimeterShapes(IEnumerable<string> shapes)
        {
            return NormalizeSet(shapes, Connected3AllowedShapes)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
        }

        private static HashSet<string> NormalizeSet(IEnumerable<string> values, IEnumerable<string> allowedValues)
        {
            HashSet<string> allowed = new HashSet<string>(
                (allowedValues ?? System.Array.Empty<string>()).Select(Normalize).Where(value => !string.IsNullOrWhiteSpace(value)),
                System.StringComparer.Ordinal);
            var result = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string value in values ?? System.Array.Empty<string>())
            {
                string normalized = Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized) && allowed.Contains(normalized))
                    result.Add(normalized);
            }

            return result;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
