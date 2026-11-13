using UnityEngine;

namespace IronRequiem
{
    public sealed class RobotArmorPiece : MonoBehaviour
    {
        [SerializeField] Transform debrisSocket;
        [SerializeField] Mesh debrisMesh;
        [SerializeField] Material interior;
        [SerializeField, Range(1, 2)] int lossStage = 1;
        public bool IsDetached { get; private set; }
        public int LossStage => lossStage;
        public Transform DebrisSocket => debrisSocket;
        public void Configure(Transform socket, Mesh mesh, int stage, Material inside) { debrisSocket = socket; debrisMesh = mesh; lossStage = stage; interior = inside; }
        public void Detach(Material material, float lifetime, LayerMask groundMask)
        {
            if (IsDetached) return;
            IsDetached = true;
            if (debrisSocket && debrisMesh)
                RobotDestructionPool.ThrowArmor(debrisMesh, material, interior, debrisSocket.position, debrisSocket.rotation, debrisSocket.lossyScale, lifetime, groundMask);
            // Disable the whole piece, including ALL its LODs. LODGroup cannot resurrect it.
            gameObject.SetActive(false);
        }
    }
}
