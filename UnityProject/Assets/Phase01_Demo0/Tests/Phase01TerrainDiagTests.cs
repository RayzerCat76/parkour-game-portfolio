using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Regression guard for the reported "spawning into the void": every branch must end up with real
    /// walkable geometry and collision, even if its serialized mesh asset is stale or missing. The
    /// runtime fallback builds the mesh from the authored profile in that case.
    ///
    /// The fallback only ever covered the collision half of that bug. Branch 1's serialized mesh was
    /// missing because the scene builder carried a stale copy of Branch 1's profile (28 authored points
    /// against 40 runtime points), so the fallback rebuilt the mesh at runtime and then assigned a
    /// null placeholder material over the renderer's authored one: the branch collided but rendered
    /// nothing at all. These checks therefore also require a real material, and require that the
    /// geometry came from the authored scene rather than from the runtime fallback.
    /// </summary>
    public sealed class Phase01TerrainIntegrityTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator EveryBranchHasWalkableGeometryAndCollision()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Assert.That(controller, Is.Not.Null);

            // A correctly authored scene never rebuilds terrain at runtime. A non-zero count means a
            // branch's mesh asset is missing from the scene, which is exactly how the void shipped.
            Assert.That(Phase01BranchTerrain.RuntimeMeshCreationCount, Is.Zero,
                "The scene must author every branch mesh; nothing should be rebuilt at runtime.");

            for (int branch = 0; branch < 4; branch++)
            {
                Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
                Assert.That(terrain, Is.Not.Null, $"Branch {branch + 1} terrain is missing.");
                Assert.That(terrain.HasValidProfile, Is.True, $"Branch {branch + 1} profile is invalid.");
                Assert.That(terrain.TopMesh, Is.Not.Null, $"Branch {branch + 1} has no walkable top mesh.");
                Assert.That(terrain.CliffMesh, Is.Not.Null, $"Branch {branch + 1} has no cliff mesh.");
                Assert.That(terrain.TopMesh.vertexCount, Is.GreaterThan(4),
                    $"Branch {branch + 1} top mesh is empty.");

                // Invisible-but-collidable terrain is what the player actually reported: the surface
                // held them up while the screen stayed black.
                Assert.That(terrain.TopRenderer, Is.Not.Null, $"Branch {branch + 1} has no top renderer.");
                Assert.That(terrain.CliffRenderer, Is.Not.Null, $"Branch {branch + 1} has no cliff renderer.");
                Assert.That(terrain.TopRenderer.sharedMaterial, Is.Not.Null,
                    $"Branch {branch + 1} walkable top has no material and would render as nothing.");
                Assert.That(terrain.CliffRenderer.sharedMaterial, Is.Not.Null,
                    $"Branch {branch + 1} cliff face has no material and would render as nothing.");

                Collider[] colliders = terrain.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders.Any(collider => collider.enabled && collider is MeshCollider), Is.True,
                    $"Branch {branch + 1} has no enabled terrain collision.");

                // The authored surface must actually be reachable by a downward probe near the spawn.
                float probe = branch == 0 ? 3f : 4f;
                Vector3 origin = controller.RadioPosition
                    + terrain.OutwardDirection * probe
                    + Vector3.up * (terrain.SampleHeight(probe) + 2f);
                Assert.That(
                    Physics.Raycast(origin, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore),
                    Is.True,
                    $"Branch {branch + 1} has no walkable surface at scalar {probe}.");
            }
        }
    }
}
