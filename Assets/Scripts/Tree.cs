using UnityEngine;

public class Tree : MonoBehaviour
{
    [SerializeField] int health = 3;
    [SerializeField] GameObject logPrefab; // kütük prefabı
    [SerializeField] Transform dropPoint;  // boş bir child (ağacın dibine koy)
    [SerializeField] float respawnTime = 10f;
    Vector3 startPos;
    Quaternion startRot;
    bool isDown;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    public void Hit(int dmg = 1)
    {
        if (isDown) return;
        health -= dmg;
        if (health <= 0) Fell();
    }

    void Fell()
    {
        isDown = true;
        // 1-2 kütük üret
        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
            Instantiate(logPrefab, dropPoint.position + Random.insideUnitSphere * 0.5f, Quaternion.identity);
        // ağacı gizle
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        GetComponent<Collider>().enabled = false;
        Invoke(nameof(Respawn), respawnTime);
    }

    void Respawn()
    {
        health = 3;
        isDown = false;
        transform.SetPositionAndRotation(startPos, startRot);
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        GetComponent<Collider>().enabled = true;
    }
}
