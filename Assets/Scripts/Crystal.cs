using Unity.Netcode;
using UnityEngine;

public class Crystal : NetworkBehaviour
{
    private void Start()
    {
        Debug.Log("[CRYSTAL] Crystal criado: " + gameObject.name);
    }
}