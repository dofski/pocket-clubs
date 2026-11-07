using PocketClubs.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketClubs.Gameplay
{
    /// <summary>
    /// Presentation and simulation host for the single authoritative football.
    /// Renders a stylized high-contrast ball mesh plus a ground-projected contact shadow
    /// so ball position and height remain readable at all times on mobile screens.
    /// </summary>
    public sealed class BallActor : MonoBehaviour
    {
        [SerializeField] private BallTuningConfig tuning;
        [SerializeField] private PitchConfig pitchConfig;

        private BallSimulation _simulation;
        private Transform _visualSphereRoot;
        private Transform _groundShadowDisc;

        public BallSimulation Simulation => _simulation;

        public void Initialize(BallTuningConfig ballTuning, PitchConfig pitchCfg, Vector3 initialPosition)
        {
            tuning = ballTuning != null ? ballTuning : BallTuningConfig.CreateRuntimeDefault();
            pitchConfig = pitchCfg != null ? pitchCfg : PitchConfig.CreateRuntimeDefault();

            _simulation = new BallSimulation(tuning, pitchConfig, initialPosition);
            EnsureVisualHierarchy();
            SyncTransformsFromSimulation(0f);
        }

        public void ResetBall(Vector3 worldPosition)
        {
            if (_simulation == null)
            {
                Initialize(tuning, pitchConfig, worldPosition);
                return;
            }

            _simulation.ResetForRestart(worldPosition);
            _simulation.CompleteRestartToFreeState();
            SyncTransformsFromSimulation(0f);
        }

        public void TickPhysics(float deltaTime)
        {
            if (_simulation == null)
            {
                return;
            }

            _simulation.StepPhysics(deltaTime);
            SyncTransformsFromSimulation(deltaTime);
        }

        public void SyncTransformsFromSimulation(float deltaTime)
        {
            if (_simulation == null)
            {
                return;
            }

            Vector3 pos = _simulation.Position;
            transform.position = pos;

            if (_visualSphereRoot != null && deltaTime > 0f)
            {
                Vector3 vel = _simulation.Velocity;
                Vector3 planarVel = new Vector3(vel.x, 0f, vel.z);
                float speed = planarVel.magnitude;
                if (speed > 0.05f && tuning.ballRadius > 0.01f)
                {
                    Vector3 rollAxis = Vector3.Cross(Vector3.up, planarVel.normalized);
                    float degrees = (speed / tuning.ballRadius) * Mathf.Rad2Deg * deltaTime;
                    _visualSphereRoot.Rotate(rollAxis, degrees, Space.World);
                }
            }

            if (_groundShadowDisc != null)
            {
                float altitude = Mathf.Max(0f, pos.y - tuning.ballRadius);
                float scaleFactor = Mathf.Clamp(1.0f - altitude * 0.18f, 0.35f, 1.0f);
                float diameter = tuning.ballRadius * 2.15f * scaleFactor;
                _groundShadowDisc.position = new Vector3(pos.x, 0.016f, pos.z);
                _groundShadowDisc.rotation = Quaternion.identity;
                _groundShadowDisc.localScale = new Vector3(diameter, 0.008f, diameter);
            }
        }

        private void EnsureVisualHierarchy()
        {
            if (_visualSphereRoot != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var whiteMat = new Material(shader) { name = "Mat_BallShell", color = new Color(0.98f, 0.98f, 0.95f) };
            if (whiteMat.HasProperty("_BaseColor")) whiteMat.SetColor("_BaseColor", whiteMat.color);

            var accentMat = new Material(shader) { name = "Mat_BallAccent", color = new Color(0.12f, 0.16f, 0.24f) };
            if (accentMat.HasProperty("_BaseColor")) accentMat.SetColor("_BaseColor", accentMat.color);

            var shadowMat = new Material(shader) { name = "Mat_BallShadow", color = new Color(0.03f, 0.08f, 0.04f) };
            if (shadowMat.HasProperty("_BaseColor")) shadowMat.SetColor("_BaseColor", shadowMat.color);

            float diameter = tuning.ballRadius * 2f;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "BallMesh";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * diameter;
            RemoveCollider(sphere);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
            _visualSphereRoot = sphere.transform;

            // Add high-contrast seams so ball rotation is visually unmistakable
            GameObject seamX = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seamX.name = "SpinBandX";
            seamX.transform.SetParent(_visualSphereRoot, false);
            seamX.transform.localPosition = Vector3.zero;
            seamX.transform.localScale = new Vector3(1.03f, 0.08f, 1.03f);
            RemoveCollider(seamX);
            seamX.GetComponent<MeshRenderer>().sharedMaterial = accentMat;

            GameObject seamZ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seamZ.name = "SpinBandZ";
            seamZ.transform.SetParent(_visualSphereRoot, false);
            seamZ.transform.localPosition = Vector3.zero;
            seamZ.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            seamZ.transform.localScale = new Vector3(1.03f, 0.08f, 1.03f);
            RemoveCollider(seamZ);
            seamZ.GetComponent<MeshRenderer>().sharedMaterial = accentMat;

            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "GroundContactShadow";
            shadow.transform.SetParent(transform, true);
            RemoveCollider(shadow);
            MeshRenderer shadowRenderer = shadow.GetComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = shadowMat;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _groundShadowDisc = shadow.transform;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }
    }
}
