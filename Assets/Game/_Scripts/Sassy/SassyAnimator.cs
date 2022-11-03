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