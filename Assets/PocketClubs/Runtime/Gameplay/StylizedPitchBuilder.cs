using PocketClubs.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketClubs.Gameplay
{
    /// <summary>
    /// Builds the original stylized low-poly 3D football pitch, pitch markings, and both goal frames.
    /// Uses shared materials and disables shadow casting on flat ground markings to keep mobile draw calls minimal.
    /// </summary>
    public sealed class StylizedPitchBuilder : MonoBehaviour
    {
        [SerializeField] private PitchConfig pitchConfig;

        private Material _turfEvenMat;
        private Material _turfOddMat;
        private Material _runoffMat;
        private Material _lineMat;
        private Material _goalFrameMat;
        private Material _goalNetMat;

        public PitchConfig Config => pitchConfig != null ? pitchConfig : (pitchConfig = PitchConfig.CreateRuntimeDefault());

        public void BuildPitchGeometry(PitchConfig configOverride = null)
        {
            if (configOverride != null)
            {
                pitchConfig = configOverride;
            }
            else if (pitchConfig == null)
            {
                pitchConfig = PitchConfig.CreateRuntimeDefault();
            }

            ClearExistingChildren();
            EnsureSharedMaterials();

            BuildTurfAndMowStripes();
            BuildPitchLines();
            BuildGoalStructure("Goal_North_AwayDefended", isNorthGoal: true);
            BuildGoalStructure("Goal_South_HomeDefended", isNorthGoal: false);
        }

        private void ClearExistingChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void EnsureSharedMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
            }

            _turfEvenMat = CreateColorMaterial(litShader, "Mat_TurfEven", new Color(0.16f, 0.49f, 0.24f), 0.1f);
            _turfOddMat = CreateColorMaterial(litShader, "Mat_TurfOdd", new Color(0.19f, 0.55f, 0.27f), 0.1f);
            _runoffMat = CreateColorMaterial(litShader, "Mat_Runoff", new Color(0.11f, 0.33f, 0.17f), 0.08f);
            _lineMat = CreateColorMaterial(litShader, "Mat_PitchLine", new Color(0.95f, 0.96f, 0.94f), 0.15f);
            _goalFrameMat = CreateColorMaterial(litShader, "Mat_GoalFrame", new Color(0.96f, 0.97f, 0.98f), 0.45f);
            _goalNetMat = CreateColorMaterial(litShader, "Mat_GoalNet", new Color(0.82f, 0.86f, 0.90f), 0.1f);
        }

        private static Material CreateColorMaterial(Shader shader, string matName, Color color, float smoothness)
        {
            var mat = new Material(shader) { name = matName, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        private void BuildTurfAndMowStripes()
        {
            var turfRoot = new GameObject("TurfSurface").transform;
            turfRoot.SetParent(transform, false);

            float totalW = pitchConfig.pitchWidth + pitchConfig.boundaryRunoff * 2f;
            float totalL = pitchConfig.pitchLength + pitchConfig.boundaryRunoff * 2f;

            CreateBoxElement(
                turfRoot,
                "RunoffApron",
                new Vector3(0f, -0.06f, 0f),
                new Vector3(totalW, 0.1f, totalL),
                _runoffMat,
                castShadows: false);

            const int stripeCount = 8;
            float stripeLength = pitchConfig.pitchLength / stripeCount;
            float startZ = -pitchConfig.HalfLength + stripeLength * 0.5f;

            for (int i = 0; i < stripeCount; i++)
            {
                float z = startZ + i * stripeLength;
                Material mat = (i % 2 == 0) ? _turfEvenMat : _turfOddMat;
                CreateBoxElement(
                    turfRoot,
                    $"MowStripe_{i}",
                    new Vector3(0f, -0.025f, z),
                    new Vector3(pitchConfig.pitchWidth, 0.05f, stripeLength),
                    mat,
                    castShadows: false);
            }
        }

        private void BuildPitchLines()
        {
            var linesRoot = new GameObject("PitchMarkings").transform;
            linesRoot.SetParent(transform, false);

            const float lineThickness = 0.14f;
            const float lineHeight = 0.012f;
            const float lineY = 0.006f;

            float halfW = pitchConfig.HalfWidth;
            float halfL = pitchConfig.HalfLength;

            // Touchlines (West & East)
            CreateBoxElement(linesRoot, "Touchline_West", new Vector3(-halfW, lineY, 0f), new Vector3(lineThickness, lineHeight, pitchConfig.pitchLength), _lineMat, false);
            CreateBoxElement(linesRoot, "Touchline_East", new Vector3(halfW, lineY, 0f), new Vector3(lineThickness, lineHeight, pitchConfig.pitchLength), _lineMat, false);

            // Goal lines (North & South)
            CreateBoxElement(linesRoot, "GoalLine_North", new Vector3(0f, lineY, halfL), new Vector3(pitchConfig.pitchWidth, lineHeight, lineThickness), _lineMat, false);
            CreateBoxElement(linesRoot, "GoalLine_South", new Vector3(0f, lineY, -halfL), new Vector3(pitchConfig.pitchWidth, lineHeight, lineThickness), _lineMat, false);

            // Halfway line & Center Spot
            CreateBoxElement(linesRoot, "HalfwayLine", new Vector3(0f, lineY, 0f), new Vector3(pitchConfig.pitchWidth, lineHeight, lineThickness), _lineMat, false);
            CreateCylinderElement(linesRoot, "CenterSpot", new Vector3(0f, lineY, 0f), new Vector3(0.45f, lineHeight * 0.5f, 0.45f), _lineMat, false);

            // Low-poly segmented Center Circle
            const int circleSegments = 16;
            const float circleRadius = 4.5f;
            float segmentArcLength = (2f * Mathf.PI * circleRadius) / circleSegments;

            for (int i = 0; i < circleSegments; i++)
            {
                float angle = (i / (float)circleSegments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * circleRadius, lineY, Mathf.Sin(angle) * circleRadius);
                var seg = CreateBoxElement(linesRoot, $"CenterCircleSeg_{i}", pos, new Vector3(lineThickness, lineHeight, segmentArcLength * 1.05f), _lineMat, false);
                seg.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }

            // North & South Penalty Boxes
            float boxWidth = Mathf.Min(pitchConfig.pitchWidth * 0.55f, 16.0f);
            float boxDepth = 8.0f;
            BuildPenaltyBoxLines(linesRoot, "NorthBox", halfL, -1f, boxWidth, boxDepth, lineThickness, lineHeight, lineY);
            BuildPenaltyBoxLines(linesRoot, "SouthBox", -halfL, 1f, boxWidth, boxDepth, lineThickness, lineHeight, lineY);
        }

        private void BuildPenaltyBoxLines(
            Transform parent,
            string prefix,
            float goalLineZ,
            float inwardSign,
            float boxWidth,
            float boxDepth,
            float thickness,
            float height,
            float y)
        {
            float halfBoxW = boxWidth * 0.5f;
            float frontZ = goalLineZ + inwardSign * boxDepth;
            float midZ = goalLineZ + inwardSign * (boxDepth * 0.5f);

            CreateBoxElement(parent, $"{prefix}_Front", new Vector3(0f, y, frontZ), new Vector3(boxWidth, height, thickness), _lineMat, false);
            CreateBoxElement(parent, $"{prefix}_Left", new Vector3(-halfBoxW, y, midZ), new Vector3(thickness, height, boxDepth), _lineMat, false);
            CreateBoxElement(parent, $"{prefix}_Right", new Vector3(halfBoxW, y, midZ), new Vector3(thickness, height, boxDepth), _lineMat, false);

            float penaltySpotZ = goalLineZ + inwardSign * (boxDepth * 0.72f);
            CreateCylinderElement(parent, $"{prefix}_PenaltySpot", new Vector3(0f, y, penaltySpotZ), new Vector3(0.35f, height * 0.5f, 0.35f), _lineMat, false);
        }

        private void BuildGoalStructure(string goalName, bool isNorthGoal)
        {
            var goalRoot = new GameObject(goalName).transform;
            goalRoot.SetParent(transform, false);

            float sign = isNorthGoal ? 1f : -1f;
            float lineZ = pitchConfig.HalfLength * sign;
            float backZ = (pitchConfig.HalfLength + pitchConfig.goalDepth) * sign;
            float midZ = (pitchConfig.HalfLength + pitchConfig.goalDepth * 0.5f) * sign;
            float halfW = pitchConfig.HalfGoalWidth;
            float h = pitchConfig.goalHeight;
            const float postDiameter = 0.18f;

            // Front Posts & Crossbar
            CreateCylinderElement(goalRoot, "LeftPost", new Vector3(-halfW, h * 0.5f, lineZ), new Vector3(postDiameter, h * 0.5f, postDiameter), _goalFrameMat, true);
            CreateCylinderElement(goalRoot, "RightPost", new Vector3(halfW, h * 0.5f, lineZ), new Vector3(postDiameter, h * 0.5f, postDiameter), _goalFrameMat, true);
            CreateBoxElement(goalRoot, "Crossbar", new Vector3(0f, h, lineZ), new Vector3(pitchConfig.goalWidth + postDiameter, postDiameter, postDiameter), _goalFrameMat, true);

            // Back Frame & Net Visual Panels
            CreateBoxElement(goalRoot, "BackBottomBar", new Vector3(0f, 0.06f, backZ), new Vector3(pitchConfig.goalWidth, 0.10f, 0.10f), _goalFrameMat, false);
            CreateBoxElement(goalRoot, "BackNetPanel", new Vector3(0f, h * 0.5f, backZ), new Vector3(pitchConfig.goalWidth, h, 0.04f), _goalNetMat, false);
            CreateBoxElement(goalRoot, "TopNetPanel", new Vector3(0f, h, midZ), new Vector3(pitchConfig.goalWidth, 0.04f, pitchConfig.goalDepth), _goalNetMat, false);
            CreateBoxElement(goalRoot, "LeftNetPanel", new Vector3(-halfW, h * 0.5f, midZ), new Vector3(0.04f, h, pitchConfig.goalDepth), _goalNetMat, false);
            CreateBoxElement(goalRoot, "RightNetPanel", new Vector3(halfW, h * 0.5f, midZ), new Vector3(0.04f, h, pitchConfig.goalDepth), _goalNetMat, false);
        }

        private static GameObject CreateBoxElement(Transform parent, string elementName, Vector3 localPos, Vector3 scale, Material mat, bool castShadows)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = elementName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }

            return go;
        }

        private static GameObject CreateCylinderElement(Transform parent, string elementName, Vector3 localPos, Vector3 scale, Material mat, bool castShadows)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = elementName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }

            return go;
        }
    }
}
