using System;
using static Helpers;
using UnityEngine;

namespace Game._Scripts.Sassy
{
    public enum MeshIds
    {
        SassyMesh,
        TwinBlade
    };

    public static class AnimStates
    {
        public static readonly string idle = "Idle";
        public static readonly string walking = "Walking";
        public static readonly string charging = "Charging";
        public static readonly string launching = "Launching";
        public static readonly string slashing = "Slashing";
        public static readonly string stunned = "Stunned";
    }

    public class SassyAnimator : MonoBehaviour
    {
        [SerializeField] private GameObject[] _meshes;

        private void Start()
        {
        }

        public void ChangeMeshes(GameObject meshesContainer, int meshId)
        {
            meshesContainer.transform.rotation = Quaternion.identity;
            Helpers.DestroyChildren(meshesContainer.transform);
            var mesh = _meshes[meshId];
            Instantiate(mesh, meshesContainer.transform.position,
                mesh.transform.rotation, meshesContainer.transform);
        }
    }
}