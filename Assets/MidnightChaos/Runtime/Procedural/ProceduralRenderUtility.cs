using System.Collections.Generic;
using MidnightChaos.World;
using UnityEngine;

namespace MidnightChaos.Procedural
{
    internal static class ProceduralRenderUtility
    {
        public const string VegetationLayerName = "Vegetation";
        public const string GrassLayerName = "Grass";
        public const string TreeLayerName = "Tree";
        public const string SmallPropLayerName = "SmallProp";
        public const string ResourceLayerName = "Resource";

        private static readonly HashSet<string> MissingLayerWarnings =
            new HashSet<string>();

        public static void ConfigureCamera(
            Camera camera,
            ProceduralRenderingSettings settings,
            Object context)
        {
            if (camera == null || settings == null)
            {
                return;
            }

            camera.farClipPlane = Mathf.Max(
                camera.nearClipPlane + 1f,
                settings.CameraFarClipPlane);

            if (!settings.UseLayerDistanceCulling)
            {
                return;
            }

            float[] distances = camera.layerCullDistances;
            if (distances == null || distances.Length != 32)
            {
                distances = new float[32];
            }
            else
            {
                distances = (float[])distances.Clone();
            }

            SetLayerDistance(
                distances,
                VegetationLayerName,
                settings.VegetationCullDistance,
                context);
            SetLayerDistance(
                distances,
                GrassLayerName,
                settings.GrassCullDistance,
                context);
            SetLayerDistance(
                distances,
                TreeLayerName,
                settings.TreeCullDistance,
                context);
            SetLayerDistance(
                distances,
                SmallPropLayerName,
                settings.SmallPropCullDistance,
                context);
            SetLayerDistance(
                distances,
                ResourceLayerName,
                settings.ResourceCullDistance,
                context);

            camera.layerCullDistances = distances;
            camera.layerCullSpherical = true;
        }

        public static int ResolveCategoryLayer(
            WorldObjectCategory category,
            Object context)
        {
            return category switch
            {
                WorldObjectCategory.Vegetation =>
                    ResolveLayer(VegetationLayerName, 2, context),
                WorldObjectCategory.Grass =>
                    ResolveLayer(GrassLayerName, 2, context),
                WorldObjectCategory.Tree =>
                    ResolveLayer(TreeLayerName, 0, context),
                WorldObjectCategory.Rock =>
                    ResolveLayer(ResourceLayerName, 0, context),
                WorldObjectCategory.Ore =>
                    ResolveLayer(ResourceLayerName, 0, context),
                _ => 0
            };
        }

        public static int ResolveLayer(
            string layerName,
            int fallbackLayer,
            Object context)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                return layer;
            }

            WarnMissingLayer(layerName, fallbackLayer, context);
            return fallbackLayer;
        }

        private static void SetLayerDistance(
            float[] distances,
            string layerName,
            float distance,
            Object context)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                WarnMissingLayer(layerName, 0, context);
                return;
            }

            distances[layer] = Mathf.Max(1f, distance);
        }

        private static void WarnMissingLayer(
            string layerName,
            int fallbackLayer,
            Object context)
        {
            if (!MissingLayerWarnings.Add(layerName))
            {
                return;
            }

            Debug.LogWarning(
                $"[Procedural] Layer '{layerName}' is missing. " +
                $"Using layer {fallbackLayer} as fallback; run the " +
                "procedural project setup before profiling.",
                context);
        }
    }
}
